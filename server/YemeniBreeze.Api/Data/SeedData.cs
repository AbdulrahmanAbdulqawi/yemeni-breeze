using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using YemeniBreeze.Api.Domain;

namespace YemeniBreeze.Api.Data;

public static class SeedData
{
    public static async Task RunAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        var adminEmail = config["Admin:Email"] ?? "admin@yemenibreeze.nl";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var password = config["Admin:Password"] ?? "ChangeMe!2026";
            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Failed to seed admin user: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await db.Events.AnyAsync())
        {
            db.Events.AddRange(
                new Event
                {
                    Slug = "the-launch",
                    TitleEn = "The Launch",
                    TitleNl = "De Lancering",
                    TitleAr = "الانطلاقة",
                    DescriptionEn = "Our first event, held close to September 26th — Yemeni National Day — as a statement of identity and pride. 150 guests from all nationalities experienced Yemeni culture firsthand: live music by Yemeni artists, traditional Yemeni coffee, and an evening built around celebrating who we are. Over 350 people were on the waiting list.",
                    DescriptionNl = "Ons eerste evenement, gehouden vlak bij 26 september — de Jemenitische Nationale Dag — als statement van identiteit en trots. 150 gasten van alle nationaliteiten beleefden de Jemenitische cultuur van dichtbij: livemuziek van Jemenitische artiesten, traditionele Jemenitische koffie en een avond in het teken van wie wij zijn. Meer dan 350 mensen stonden op de wachtlijst.",
                    DescriptionAr = "فعاليتنا الأولى، أُقيمت قرب السادس والعشرين من سبتمبر — العيد الوطني اليمني — كرسالة هوية وفخر. عاش 150 ضيفًا من مختلف الجنسيات الثقافة اليمنية عن قرب: موسيقى حية لفنانين يمنيين، وقهوة يمنية تقليدية، وأمسية احتفاءً بمن نحن. وكان أكثر من 350 شخصًا على قائمة الانتظار.",
                    Date = new DateOnly(2024, 9, 28),
                    StartTime = "18:00",
                    EndTime = "23:00",
                    Location = "Amsterdam",
                    Capacity = 150,
                    Status = EventStatus.Past,
                    IsRegistrationOpen = false
                },
                new Event
                {
                    Slug = "cooking-for-the-camps",
                    TitleEn = "Cooking for the Camps",
                    TitleNl = "Koken voor de Kampen",
                    TitleAr = "الطبخ من أجل المخيمات",
                    DescriptionEn = "Guests learned about Yemeni culture and cooked traditional Yemeni food alongside our team. Together we prepared 350 meals, every single one sent directly to residents of local refugee camps with a handwritten message of love and welcome. Then 100 people sat together and shared the meal as one — hosts and guests, Dutch and Yemeni, side by side. This was not charity. It was connection.",
                    DescriptionNl = "Gasten maakten kennis met de Jemenitische cultuur en kookten traditionele Jemenitische gerechten samen met ons team. Samen bereidden we 350 maaltijden, die stuk voor stuk rechtstreeks naar bewoners van lokale AZC's gingen, met een handgeschreven boodschap van liefde en welkom. Daarna zaten 100 mensen samen aan tafel — gastheren en gasten, Nederlanders en Jemenieten, zij aan zij. Dit was geen liefdadigheid. Dit was verbinding.",
                    DescriptionAr = "تعرّف الضيوف على الثقافة اليمنية وطبخوا أطباقًا يمنية تقليدية جنبًا إلى جنب مع فريقنا. أعددنا معًا 350 وجبة، أُرسلت كل واحدة منها مباشرة إلى سكان مخيمات اللاجئين المحلية مع رسالة مكتوبة بخط اليد مليئة بالحب والترحيب. ثم جلس 100 شخص معًا لمشاركة الطعام — مضيفين وضيوفًا، هولنديين ويمنيين، جنبًا إلى جنب. لم يكن هذا عملاً خيريًا، بل كان تواصلًا.",
                    Date = new DateOnly(2025, 3, 7),
                    StartTime = "15:30",
                    EndTime = "19:00",
                    Location = "A Beautiful Mess, Amsterdam",
                    Capacity = 100,
                    Status = EventStatus.Past,
                    IsRegistrationOpen = false
                },
                new Event
                {
                    Slug = "cooking-elthetokerk",
                    TitleEn = "Cooking at the Elthetokerk",
                    TitleNl = "Koken in de Elthetokerk",
                    TitleAr = "الطبخ في كنيسة إلثيتو",
                    DescriptionEn = "For our third event, we cooked for 150 people in need at a local church in Amsterdam. An intimate and private gathering — by the nature of the venue and the people we were serving, we chose not to document or promote this event publicly. The food was Yemeni. The welcome was unconditional.",
                    DescriptionNl = "Voor ons derde evenement kookten we voor 150 mensen in nood in een lokale kerk in Amsterdam. Een intieme en besloten bijeenkomst — vanwege de aard van de locatie en de mensen die we bedienden, kozen we ervoor dit evenement niet publiekelijk te documenteren of promoten. Het eten was Jemenitisch. Het welkom was onvoorwaardelijk.",
                    DescriptionAr = "في فعاليتنا الثالثة، طبخنا لـ150 شخصًا من المحتاجين في كنيسة محلية في أمستردام. كان لقاءً حميميًا وخاصًا — وبحكم طبيعة المكان ومن نخدمهم، اخترنا عدم توثيق هذه الفعالية أو الترويج لها علنًا. كان الطعام يمنيًا، وكان الترحيب غير مشروط.",
                    Date = new DateOnly(2025, 5, 8),
                    StartTime = "16:00",
                    EndTime = "20:00",
                    Location = "Elthetokerk, Amsterdam",
                    Capacity = 150,
                    Status = EventStatus.Past,
                    IsRegistrationOpen = false
                },
                new Event
                {
                    Slug = "yemeni-breeze-sofra",
                    TitleEn = "Yemeni Breeze Sofra",
                    TitleNl = "Yemeni Breeze Sofra",
                    TitleAr = "سفرة نسمات اليمن",
                    DescriptionEn = "A communal dinner bringing Dutch volunteers, refugees, and organizers together. Cook side by side, prepare meals for residents of asylum seeker centers — each with a personal message — then sit together at one table and share the moment as equals. Hands-on Yemeni cooking workshops, traditional clothing, and cultural sharing through food and conversation.",
                    DescriptionNl = "Een gezamenlijk diner dat Nederlandse vrijwilligers, vluchtelingen en organisatoren samenbrengt. Kook zij aan zij, bereid maaltijden voor bewoners van AZC's — elk met een persoonlijke boodschap — en schuif daarna samen aan één tafel om het moment als gelijken te delen. Praktische Jemenitische kookworkshops, traditionele kleding en culturele uitwisseling via eten en gesprek.",
                    DescriptionAr = "عشاء جماعي يجمع المتطوعين الهولنديين واللاجئين والمنظمين معًا. اطبخوا جنبًا إلى جنب، وأعدّوا وجبات لسكان مراكز طالبي اللجوء — كل وجبة برسالة شخصية — ثم اجلسوا معًا على طاولة واحدة وتشاركوا اللحظة سواسية. ورش طبخ يمنية تفاعلية، وأزياء تقليدية، وتبادل ثقافي عبر الطعام والحوار.",
                    Date = new DateOnly(2026, 3, 7),
                    StartTime = "15:30",
                    EndTime = "19:00",
                    Location = "A Beautiful Mess, Amsterdam",
                    Capacity = 100,
                    Status = EventStatus.Published,
                    IsRegistrationOpen = true
                });
            await db.SaveChangesAsync();
        }
    }
}
