import { Component, ElementRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { Lang, LanguageService, SITE_LANGS } from '../../core/language.service';
import { LocalizedDatePipe } from '../../core/localized-date.pipe';
import { FeedbackAnswers, FeedbackForm } from '../../core/models';
import { FEEDBACK_OPTIONS, FeedbackListKey, emptyAnswers } from './feedback-options';

type PageState = 'loading' | 'ready' | 'notFound' | 'error';

/**
 * The after-event feedback form, reached from the private link in the feedback email.
 * Three short steps; each "Next" saves what's there, so answers aren't lost if someone
 * stops halfway. Every question is optional.
 */
@Component({
  selector: 'app-feedback-page',
  imports: [TranslocoPipe, LocalizedDatePipe, RouterLink, FormsModule],
  template: `
    <section class="section-dark fb-head">
      <div class="container fb-narrow">
        @if (form(); as f) {
          <h1>{{ 'feedback.thanksName' | transloco: { name: f.firstName } }}</h1>
          <p class="fb-event">{{ title() }} · {{ f.event.date | localizedDate: 'long' }}</p>
          @if (!finished()) {
            <ol class="fb-steps" [attr.aria-label]="'feedback.progress' | transloco">
              @for (s of [1, 2, 3]; track s) {
                <li [class.done]="s < step()" [class.current]="s === step()" [attr.aria-current]="s === step() ? 'step' : null">
                  <span class="bar"></span>
                  <span class="label">{{ 'feedback.step' + s | transloco }}</span>
                </li>
              }
            </ol>
          }
        } @else {
          <h1>{{ 'feedback.title' | transloco }}</h1>
        }
      </div>
    </section>

    <section class="section fb-body">
      <div class="container fb-narrow" #top>
        @switch (state()) {
          @case ('loading') {
            <p class="muted">{{ 'feedback.loading' | transloco }}</p>
          }
          @case ('notFound') {
            <div class="card card-form">
              <h2>{{ 'feedback.notFoundTitle' | transloco }}</h2>
              <p>{{ 'feedback.notFoundText' | transloco }}</p>
              <a routerLink="/contact" class="btn btn-outline">{{ 'feedback.contactUs' | transloco }}</a>
            </div>
          }
          @case ('error') {
            <p class="error-text">{{ 'feedback.error' | transloco }}</p>
          }
          @case ('ready') {
            @if (finished()) {
              <div class="card card-form fb-thanks" animate.enter="yb-rise-in">
                <span class="heart" aria-hidden="true">❤️</span>
                <h2>{{ 'feedback.doneTitle' | transloco }}</h2>
                <p>{{ 'feedback.doneText1' | transloco }}</p>
                <p>{{ 'feedback.doneText2' | transloco }}</p>
                <p class="together">{{ 'feedback.doneTogether' | transloco }}</p>
                <div class="fb-actions centered">
                  <a routerLink="/events" class="btn btn-primary">{{ 'feedback.seeEvents' | transloco }}</a>
                  <button type="button" class="btn btn-outline" (click)="edit()">{{ 'feedback.edit' | transloco }}</button>
                </div>
              </div>
            } @else {
              <form class="card card-form fb-card" (ngSubmit)="next()">
                @switch (step()) {
                  @case (1) {
                    <div animate.enter="yb-rise-in">
                      <p class="fb-intro">{{ 'feedback.intro' | transloco }}</p>
                      <p class="fb-optional">{{ 'feedback.optional' | transloco }}</p>

                      <fieldset class="fb-q">
                        <legend>{{ 'feedback.q1' | transloco }}</legend>
                        <p class="fb-hint">{{ 'feedback.pickAny' | transloco }}</p>
                        <div class="chips">
                          @for (code of options.expectations; track code) {
                            <label class="chip">
                              <input type="checkbox" [checked]="has('expectations', code)" (change)="toggle('expectations', code)" />
                              <span>{{ 'feedback.options.expectations.' + code | transloco }}</span>
                            </label>
                          }
                        </div>
                        @if (has('expectations', 'other')) {
                          <input class="fb-other" name="expectationsOther" [(ngModel)]="a.expectationsOther" maxlength="2000"
                                 [placeholder]="'feedback.otherPlaceholder' | transloco" [attr.aria-label]="'feedback.otherPlaceholder' | transloco" />
                        }
                      </fieldset>

                      <fieldset class="fb-q">
                        <legend>{{ 'feedback.q2' | transloco }}</legend>
                        <div class="chips">
                          @for (code of options.met; track code) {
                            <label class="chip">
                              <input type="radio" name="met" [value]="code" [(ngModel)]="a.metExpectations" />
                              <span>{{ 'feedback.options.met.' + code | transloco }}</span>
                            </label>
                          }
                        </div>
                      </fieldset>

                      @if (a.metExpectations && a.metExpectations !== 'yes') {
                        <div class="fb-q" animate.enter="yb-rise-in">
                          <label for="improvements">{{ 'feedback.q3' | transloco }}</label>
                          <p class="fb-hint">{{ 'feedback.q3Hint' | transloco }}</p>
                          <textarea id="improvements" name="improvements" rows="3" maxlength="2000" [(ngModel)]="a.improvements"></textarea>
                        </div>
                      }

                      <div class="fb-q">
                        <label for="favouritePart">{{ 'feedback.q4' | transloco }}</label>
                        <p class="fb-hint">{{ 'feedback.q4Hint' | transloco }}</p>
                        <textarea id="favouritePart" name="favouritePart" rows="3" maxlength="2000" [(ngModel)]="a.favouritePart"></textarea>
                      </div>
                    </div>
                  }

                  @case (2) {
                    <div class="fb-join" animate.enter="yb-rise-in">
                      <h2>{{ 'feedback.joinTitle' | transloco }}</h2>
                      <p class="fb-join-intro">{{ 'feedback.joinIntro' | transloco }}</p>

                      <fieldset class="fb-q">
                        <legend>{{ 'feedback.q5' | transloco }}</legend>
                        <div class="chips">
                          @for (code of options.join; track code) {
                            <label class="chip">
                              <input type="radio" name="join" [value]="code" [(ngModel)]="a.joinCommunity" />
                              <span>{{ 'feedback.options.join.' + code | transloco }}</span>
                            </label>
                          }
                        </div>
                      </fieldset>

                      <fieldset class="fb-q">
                        <legend>{{ 'feedback.q6' | transloco }}</legend>
                        <p class="fb-hint">{{ 'feedback.q6Hint' | transloco }}</p>
                        <div class="chips">
                          @for (code of options.contributions; track code) {
                            <label class="chip">
                              <input type="checkbox" [checked]="has('contributions', code)" (change)="toggle('contributions', code)" />
                              <span>{{ 'feedback.options.contributions.' + code | transloco }}</span>
                            </label>
                          }
                        </div>
                        @if (has('contributions', 'other')) {
                          <input class="fb-other" name="contributionsOther" [(ngModel)]="a.contributionsOther" maxlength="2000"
                                 [placeholder]="'feedback.otherPlaceholder' | transloco" [attr.aria-label]="'feedback.otherPlaceholder' | transloco" />
                        }
                      </fieldset>
                    </div>
                  }

                  @case (3) {
                    <div animate.enter="yb-rise-in">
                      <p class="fb-intro">{{ 'feedback.ideasIntro' | transloco }}</p>

                      <div class="fb-q">
                        <label for="dishes">{{ 'feedback.q7' | transloco }}</label>
                        <textarea id="dishes" name="dishes" rows="2" maxlength="2000" [(ngModel)]="a.dishes"></textarea>
                      </div>
                      <div class="fb-q">
                        <label for="regionalFood">{{ 'feedback.q8' | transloco }}</label>
                        <textarea id="regionalFood" name="regionalFood" rows="2" maxlength="2000" [(ngModel)]="a.regionalFood"></textarea>
                      </div>

                      <fieldset class="fb-q">
                        <legend>{{ 'feedback.q9' | transloco }}</legend>
                        <p class="fb-hint">{{ 'feedback.pickAny' | transloco }}</p>
                        <div class="chips">
                          @for (code of options.music; track code) {
                            <label class="chip">
                              <input type="checkbox" [checked]="has('music', code)" (change)="toggle('music', code)" />
                              <span>{{ 'feedback.options.music.' + code | transloco }}</span>
                            </label>
                          }
                        </div>
                      </fieldset>

                      <fieldset class="fb-q">
                        <legend>{{ 'feedback.q10' | transloco }}</legend>
                        <p class="fb-hint">{{ 'feedback.pickAny' | transloco }}</p>
                        <div class="chips">
                          @for (code of options.dances; track code) {
                            <label class="chip">
                              <input type="checkbox" [checked]="has('dances', code)" (change)="toggle('dances', code)" />
                              <span>{{ 'feedback.options.dances.' + code | transloco }}</span>
                            </label>
                          }
                        </div>
                        @if (has('dances', 'other')) {
                          <input class="fb-other" name="dancesOther" [(ngModel)]="a.dancesOther" maxlength="2000"
                                 [placeholder]="'feedback.otherPlaceholder' | transloco" [attr.aria-label]="'feedback.otherPlaceholder' | transloco" />
                        }
                      </fieldset>

                      <div class="fb-q">
                        <label for="traditions">{{ 'feedback.q11' | transloco }}</label>
                        <textarea id="traditions" name="traditions" rows="2" maxlength="2000" [(ngModel)]="a.traditions"></textarea>
                      </div>
                      <div class="fb-q">
                        <label for="missing">{{ 'feedback.q12' | transloco }}</label>
                        <textarea id="missing" name="missing" rows="2" maxlength="2000" [(ngModel)]="a.missing"></textarea>
                      </div>
                      <div class="fb-q">
                        <label for="engagement">{{ 'feedback.q13' | transloco }}</label>
                        <textarea id="engagement" name="engagement" rows="2" maxlength="2000" [(ngModel)]="a.engagement"></textarea>
                      </div>
                      <div class="fb-q">
                        <label for="ideas">{{ 'feedback.q14' | transloco }}</label>
                        <textarea id="ideas" name="ideas" rows="2" maxlength="2000" [(ngModel)]="a.ideas"></textarea>
                      </div>
                      <div class="fb-q">
                        <label for="anythingElse">{{ 'feedback.q15' | transloco }}</label>
                        <textarea id="anythingElse" name="anythingElse" rows="3" maxlength="2000" [(ngModel)]="a.anythingElse"></textarea>
                      </div>
                    </div>
                  }
                }

                @if (saveError()) {
                  <p class="error-text" role="alert">{{ 'feedback.saveError' | transloco }}</p>
                }
                <div class="fb-actions">
                  @if (step() > 1) {
                    <button type="button" class="btn btn-outline" [disabled]="saving()" (click)="back()">
                      {{ 'feedback.back' | transloco }}
                    </button>
                  }
                  <span class="fb-stepcount">{{ 'feedback.stepOf' | transloco: { n: step() } }}</span>
                  <button type="submit" class="btn btn-primary" [disabled]="saving()">
                    {{ (saving() ? 'feedback.saving' : step() === 3 ? 'feedback.send' : 'feedback.next') | transloco }}
                  </button>
                </div>
              </form>
            }
          }
        }
      </div>
    </section>
  `,
  styles: `
    .fb-narrow {
      max-width: 720px;
    }

    .fb-head {
      padding: 3rem 0 2.2rem;

      h1 {
        font-size: clamp(1.8rem, 4vw, 2.6rem);
        margin: 0;
        color: var(--yb-gold);
      }
    }

    .fb-event {
      margin: 0.4rem 0 0;
      opacity: 0.9;
    }

    .fb-steps {
      list-style: none;
      display: flex;
      gap: 0.5rem;
      padding: 0;
      margin: 1.6rem 0 0;

      li {
        flex: 1;
        min-width: 0;
      }

      .bar {
        display: block;
        height: 5px;
        border-radius: 3px;
        background: var(--yb-brown-soft);
      }

      .label {
        display: block;
        margin-top: 0.45rem;
        font-size: 0.8rem;
        color: var(--yb-outline);
      }

      li.done .bar,
      li.current .bar {
        background: var(--yb-gold-dim);
      }

      li.current .label {
        color: var(--yb-gold);
        font-weight: 700;
      }
    }

    .fb-body {
      padding-top: 2.5rem;
    }

    .fb-card {
      border-top: 4px solid var(--yb-gold-dim);
    }

    .fb-intro {
      font-size: 1.05rem;
      line-height: 1.65;
      margin: 0 0 0.4rem;
    }

    .fb-optional,
    .fb-hint,
    .muted {
      color: var(--yb-maroon);
      font-size: 0.9rem;
      margin: 0 0 0.6rem;
    }

    .fb-optional {
      margin-bottom: 1.8rem;
    }

    .fb-q {
      border: none;
      padding: 0;
      margin: 0 0 1.8rem;
      min-width: 0;

      legend,
      label[for] {
        display: block;
        font-family: var(--yb-font-heading);
        font-size: 1.15rem;
        font-weight: 700;
        color: var(--yb-brown);
        padding: 0;
        margin-bottom: 0.5rem;
      }

      textarea,
      .fb-other {
        width: 100%;
        box-sizing: border-box;
        font-family: var(--yb-font-body);
        font-size: 1rem;
        padding: 0.7rem 0.9rem;
        border: 1.5px solid var(--yb-outline);
        border-radius: 10px;
        background: #fff;
        color: var(--yb-brown-deep);
        resize: vertical;

        &:focus {
          outline: 2px solid var(--yb-blue);
          border-color: var(--yb-blue);
        }
      }

      .fb-other {
        margin-top: 0.4rem;
      }
    }

    .chips {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }

    .chip {
      position: relative;
      cursor: pointer;

      input {
        position: absolute;
        opacity: 0;
        width: 1px;
        height: 1px;
      }

      span {
        display: inline-block;
        padding: 0.5rem 1rem;
        border: 1.5px solid var(--yb-outline);
        border-radius: 999px;
        background: #fff;
        color: var(--yb-brown-deep);
        font-size: 0.95rem;
        line-height: 1.35;
        transition: background 0.15s ease, border-color 0.15s ease, color 0.15s ease;
      }

      &:hover span {
        border-color: var(--yb-maroon);
      }

      input:checked + span {
        background: var(--yb-brown);
        border-color: var(--yb-brown);
        color: var(--yb-gold);
      }

      input:focus-visible + span {
        outline: 2px solid var(--yb-blue);
        outline-offset: 2px;
      }
    }

    /* Step 2: same light card and chips as step 1, with a heading to open the invitation */
    .fb-join {
      h2 {
        color: var(--yb-maroon);
        margin: 0 0 0.4rem;
        font-size: clamp(1.4rem, 3vw, 1.8rem);
      }

      .fb-join-intro {
        font-size: 1.05rem;
        margin: 0 0 1.8rem;
        line-height: 1.65;
      }
    }

    .fb-actions {
      display: flex;
      align-items: center;
      gap: 0.8rem;
      flex-wrap: wrap;
      margin-top: 0.5rem;

      .btn-primary {
        margin-inline-start: auto;
      }

      &.centered {
        justify-content: center;

        .btn-primary {
          margin-inline-start: 0;
        }
      }
    }

    .fb-stepcount {
      color: var(--yb-maroon);
      font-size: 0.85rem;
    }

    .fb-thanks {
      text-align: center;

      .heart {
        font-size: 2.6rem;
      }

      h2 {
        margin: 0.4rem 0 1rem;
      }

      p {
        line-height: 1.7;
      }

      .together {
        font-family: var(--yb-font-heading);
        font-size: 1.2rem;
        color: var(--yb-maroon);
        font-weight: 700;
      }
    }

    @media (max-width: 560px) {
      .card-form {
        padding: 1.4rem 1.1rem;
      }

      .fb-steps .label {
        font-size: 0.72rem;
      }
    }
  `
})
export class FeedbackPage {
  private api = inject(ApiService);
  private lang = inject(LanguageService);

  readonly token = input.required<string>();
  /** One-tap answer from the email: ?met=yes|partly|no */
  readonly met = input<string>();
  /** One-tap answer from the email: ?join=yes|maybe|no */
  readonly join = input<string>();
  /** The email's "Take part" button opens the form at the take-part step: ?step=2 */
  readonly startStep = input<string>(undefined, { alias: 'step' });
  /** The guest's language, carried in the email link: ?lang=ar */
  readonly langParam = input<string>(undefined, { alias: 'lang' });

  private readonly topRef = viewChild<ElementRef<HTMLElement>>('top');

  readonly options = FEEDBACK_OPTIONS;
  readonly state = signal<PageState>('loading');
  readonly form = signal<FeedbackForm | null>(null);
  readonly step = signal(1);
  readonly finished = signal(false);
  readonly saving = signal(false);
  readonly saveError = signal(false);

  /** Bound to the inputs with ngModel; saved on each step. */
  a: FeedbackAnswers = emptyAnswers();

  readonly title = computed(() => {
    const ev = this.form()?.event;
    if (!ev) return '';
    const lang = this.lang.current();
    return (lang === 'nl' && ev.titleNl) || (lang === 'ar' && ev.titleAr) || ev.titleEn;
  });

  constructor() {
    effect(() => {
      const requested = this.langParam() as Lang | undefined;
      if (requested && SITE_LANGS.includes(requested)) untracked(() => this.lang.set(requested));
    });
    effect(() => {
      const token = this.token();
      untracked(() => this.load(token));
    });
  }

  private load(token: string) {
    this.state.set('loading');
    this.api.getFeedbackForm(token).subscribe({
      next: form => {
        this.form.set(form);
        this.a = { ...emptyAnswers(), ...(form.answers ?? {}) };
        this.finished.set(form.isComplete);
        // "Take part" from the email: go straight to that step, even after they finished,
        // so they can add or change how they'd like to take part.
        if (this.startStep() === '2') {
          this.finished.set(false);
          this.step.set(2);
        }
        this.state.set('ready');

        // They tapped an answer in the email: keep it right away, even if they go no further.
        // A tap is a deliberate choice, so it replaces an earlier answer to the same question.
        let tappedChanged = false;
        const met = this.met();
        if (met && (FEEDBACK_OPTIONS.met as readonly string[]).includes(met) && this.a.metExpectations !== met) {
          this.a.metExpectations = met;
          tappedChanged = true;
        }
        const join = this.join();
        if (join && (FEEDBACK_OPTIONS.join as readonly string[]).includes(join) && this.a.joinCommunity !== join) {
          this.a.joinCommunity = join;
          tappedChanged = true;
        }
        if (tappedChanged) this.api.saveFeedback(token, this.a, false).subscribe({ error: () => {} });
      },
      error: err => this.state.set(err.status === 404 || err.status === 400 ? 'notFound' : 'error')
    });
  }

  has(list: FeedbackListKey, code: string) {
    return this.a[list].includes(code);
  }

  toggle(list: FeedbackListKey, code: string) {
    const current = this.a[list];
    this.a[list] = current.includes(code) ? current.filter(c => c !== code) : [...current, code];
  }

  next() {
    const last = this.step() === 3;
    this.saving.set(true);
    this.saveError.set(false);
    this.api.saveFeedback(this.token(), this.a, last).subscribe({
      next: () => {
        this.saving.set(false);
        if (last) this.finished.set(true);
        else this.step.update(s => s + 1);
        this.scrollUp();
      },
      error: () => {
        this.saving.set(false);
        this.saveError.set(true);
      }
    });
  }

  back() {
    this.saveError.set(false);
    this.step.update(s => Math.max(1, s - 1));
    this.scrollUp();
  }

  edit() {
    this.finished.set(false);
    this.step.set(1);
    this.scrollUp();
  }

  private scrollUp() {
    const el = this.topRef()?.nativeElement;
    if (!el) return;
    const y = el.getBoundingClientRect().top + window.scrollY - 120;
    if (window.scrollY > y) window.scrollTo({ top: Math.max(0, y), behavior: 'smooth' });
  }
}
