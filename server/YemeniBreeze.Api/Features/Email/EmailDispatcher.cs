using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Data;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Features.Email;

/// <summary>Adds emails to the outbox. The caller saves the DbContext, then calls <see cref="EmailSignal.Wake"/>.</summary>
public static class EmailOutbox
{
    public static void Enqueue(AppDbContext db, Registration registration, EmailKind kind) =>
        db.OutgoingEmails.Add(new OutgoingEmail { Registration = registration, Kind = kind });
}

/// <summary>Lets request handlers nudge the dispatcher so new emails go out right away instead of on the next poll.</summary>
public class EmailSignal
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Wake()
    {
        try { _semaphore.Release(); }
        catch (SemaphoreFullException) { /* already awake */ }
    }

    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken ct) => _semaphore.WaitAsync(timeout, ct);
}

/// <summary>Europe/Amsterdam, the time zone all events are in.</summary>
public static class EventTimeZone
{
    public static readonly TimeZoneInfo Amsterdam = Find();

    private static TimeZoneInfo Find()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam"); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}

/// <summary>
/// Sends queued emails one at a time, retrying transient failures with backoff, and queues
/// day-before reminders. Rows survive restarts, so nothing is lost if the app goes down mid-send.
/// </summary>
public class EmailDispatcher(
    IServiceScopeFactory scopes, EmailService emails, EmailSignal signal, ILogger<EmailDispatcher> logger)
    : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReminderInterval = TimeSpan.FromMinutes(10);

    /// <summary>Wait before retry n (1-based). The last value repeats.</summary>
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1), TimeSpan.FromHours(3)
    ];

    /// <summary>Reminders go out from this local hour on the day before the event.</summary>
    private const int ReminderHour = 9;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!emails.Options.DispatcherEnabled) return;

        var lastReminderRun = DateTime.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (emails.Options.RemindersEnabled && now - lastReminderRun >= ReminderInterval)
                {
                    await QueueRemindersAsync(now, stoppingToken);
                    lastReminderRun = now;
                }
                while (await ProcessDueAsync(DateTime.UtcNow, stoppingToken) > 0) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email dispatcher loop failed; retrying on the next poll");
            }

            try
            {
                await signal.WaitAsync(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Sends one batch of due emails. Returns how many were handled.</summary>
    public async Task<int> ProcessDueAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var options = emails.Options;

        var batch = await db.OutgoingEmails
            .Include(o => o.Registration!).ThenInclude(r => r.Event)
            .Where(o => o.Status == EmailStatus.Pending && o.NextAttemptAt <= nowUtc)
            .OrderBy(o => o.NextAttemptAt).ThenBy(o => o.Id)
            .Take(20)
            .ToListAsync(ct);

        foreach (var item in batch)
        {
            try
            {
                await SendOneAsync(item, options, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A bug or bad data, not a provider hiccup: count it so one bad row can't loop forever.
                item.Attempts++;
                Fail(item, options, ex);
            }
            await db.SaveChangesAsync(ct);
            if (options.SendIntervalMs > 0) await Task.Delay(options.SendIntervalMs, ct);
        }
        return batch.Count;
    }

    private async Task SendOneAsync(OutgoingEmail item, EmailOptions options, CancellationToken ct)
    {
        var registration = item.Registration!;
        var ev = registration.Event!;
        registration.ManageToken ??= Guid.NewGuid();

        var content = RegistrationEmails.Build(registration, ev, item.Kind, options.PublicBaseUrl);
        if (content is null)
        {
            item.Status = EmailStatus.Skipped;
            item.LastError = $"Registration is {registration.Status}; the {item.Kind} email no longer applies.";
            return;
        }

        if (!emails.IsConfigured)
        {
            item.Status = EmailStatus.Skipped;
            item.LastError = "No email provider configured.";
            logger.LogInformation("Email not configured — would send '{Subject}' to {Email}",
                content.Subject, registration.Email);
            return;
        }

        item.Attempts++;
        try
        {
            await emails.SendAsync(registration.FullName, registration.Email, content, ct);
            item.Status = EmailStatus.Sent;
            item.SentAt = DateTime.UtcNow;
            item.LastError = null;
            logger.LogInformation("Sent {Kind} email to {Email} via {Provider}",
                item.Kind, registration.Email, options.ResolvedProvider);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A rejected address or bad API key won't fix itself; outages and rate limits will.
            Fail(item, options, ex, giveUp: ex is EmailSendException { Transient: false });
        }
    }

    /// <summary>Schedules a retry with backoff, or gives up once the attempts are used.</summary>
    private void Fail(OutgoingEmail item, EmailOptions options, Exception ex, bool giveUp = false)
    {
        var to = item.Registration?.Email;
        var error = ex.Message;
        item.LastError = error.Length > 1000 ? error[..1000] : error;
        if (giveUp || item.Attempts >= options.MaxAttempts)
        {
            item.Status = EmailStatus.Failed;
            logger.LogError(ex, "Giving up on {Kind} email to {Email} after {Attempts} attempt(s)",
                item.Kind, to, item.Attempts);
        }
        else
        {
            item.NextAttemptAt = DateTime.UtcNow + Backoff[Math.Min(item.Attempts, Backoff.Length) - 1];
            logger.LogWarning("{Kind} email to {Email} failed (attempt {Attempts}), retrying at {Next:u}: {Error}",
                item.Kind, to, item.Attempts, item.NextAttemptAt, error);
        }
    }

    /// <summary>
    /// From 09:00 Amsterdam time on the day before an event, queues one reminder per confirmed
    /// registrant. People who registered in the last 12 hours just got their ticket, so they're skipped.
    /// </summary>
    public async Task<int> QueueRemindersAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), EventTimeZone.Amsterdam);
        if (local.Hour < ReminderHour) return 0;
        var tomorrow = DateOnly.FromDateTime(local).AddDays(1);
        var registeredBefore = nowUtc.AddHours(-12);

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var due = await db.Registrations
            .Where(r => r.Status == RegistrationStatus.Confirmed
                        && r.Event!.Date == tomorrow
                        && r.Event.Status == EventStatus.Published
                        && r.CreatedAt < registeredBefore
                        && !db.OutgoingEmails.Any(o => o.RegistrationId == r.Id && o.Kind == EmailKind.Reminder))
            .ToListAsync(ct);
        if (due.Count == 0) return 0;

        foreach (var registration in due)
            EmailOutbox.Enqueue(db, registration, EmailKind.Reminder);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Queued {Count} reminder email(s) for {Date}", due.Count, tomorrow);
        return due.Count;
    }
}
