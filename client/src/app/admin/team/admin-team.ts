import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { Lang } from '../../core/language.service';
import { TeamMemberDto, TeamMemberInput } from '../../core/models';
import { ConfirmService } from '../ui/confirm.service';
import { ToastService } from '../ui/toast.service';
import { PageHeader } from '../ui/page-header';
import { EmptyState, Spinner } from '../ui/state-views';
import { LangSelect } from '../ui/lang-select';

@Component({
  selector: 'app-admin-team',
  imports: [ReactiveFormsModule, TranslocoPipe, PageHeader, EmptyState, Spinner, LangSelect],
  template: `
    <app-page-header [heading]="'admin.team.title' | transloco" [subtitle]="'admin.team.subtitle' | transloco">
      <button class="btn btn-primary" type="button" (click)="startNew()">{{ 'admin.team.add' | transloco }}</button>
    </app-page-header>

    @if (editing()) {
      <form class="card team-form" [formGroup]="form" (ngSubmit)="save()">
        <div class="grid-2">
          <div class="field">
            <label for="name">{{ 'admin.team.name' | transloco }}</label>
            <input id="name" formControlName="name" />
          </div>
          <div class="field">
            <label for="sortOrder">{{ 'admin.team.sortOrder' | transloco }}</label>
            <input id="sortOrder" formControlName="sortOrder" type="number" />
          </div>
        </div>

        <div class="lang-block">
          <app-lang-select [(active)]="contentLang" [label]="'admin.events.translation' | transloco" />

          @switch (contentLang()) {
            @case ('nl') {
              <div class="field">
                <label for="roleNl">{{ 'admin.team.role' | transloco }}</label>
                <input id="roleNl" formControlName="roleNl" />
              </div>
              <div class="field">
                <label for="bioNl">{{ 'admin.team.bio' | transloco }}</label>
                <textarea id="bioNl" formControlName="bioNl" rows="4"></textarea>
              </div>
            }
            @case ('ar') {
              <div class="field">
                <label for="roleAr">{{ 'admin.team.role' | transloco }}</label>
                <input id="roleAr" formControlName="roleAr" dir="rtl" />
              </div>
              <div class="field">
                <label for="bioAr">{{ 'admin.team.bio' | transloco }}</label>
                <textarea id="bioAr" formControlName="bioAr" rows="4" dir="rtl"></textarea>
              </div>
            }
            @default {
              <div class="field">
                <label for="roleEn">{{ 'admin.team.role' | transloco }}</label>
                <input id="roleEn" formControlName="roleEn" />
              </div>
              <div class="field">
                <label for="bioEn">{{ 'admin.team.bio' | transloco }}</label>
                <textarea id="bioEn" formControlName="bioEn" rows="4"></textarea>
              </div>
            }
          }
          <p class="bio-hint">{{ 'admin.team.bioHint' | transloco }}</p>
        </div>

        <div class="field cover-field">
          <label>{{ 'admin.team.photo' | transloco }}</label>
          @if (form.controls.photoUrl.value; as photo) {
            <img [src]="photo" class="image-preview" alt="" />
            <button type="button" class="btn-link danger" (click)="clearPhoto()">
              {{ 'admin.gallery.removeImage' | transloco }}
            </button>
          }
          <input type="file" accept="image/*" (change)="upload($event)" [disabled]="uploading()" />
          @if (uploading()) {
            <span class="uploading-note">{{ 'admin.media.uploading' | transloco }}…</span>
          }
        </div>

        <div class="form-actions">
          <button class="btn btn-primary" type="submit" [disabled]="saving()">
            {{ 'admin.team.save' | transloco }}
          </button>
          <button class="btn btn-outline" type="button" (click)="cancelEdit()">
            {{ 'admin.team.cancel' | transloco }}
          </button>
        </div>
      </form>
    }

    @if (loading()) {
      <app-spinner />
    } @else if (members().length) {
      <div class="table-wrap">
        <table class="yb-table">
          <thead>
            <tr>
              <th></th>
              <th>{{ 'admin.team.name' | transloco }}</th>
              <th>{{ 'admin.team.role' | transloco }}</th>
              <th>{{ 'admin.team.sortOrder' | transloco }}</th>
              <th>{{ 'admin.team.hasBio' | transloco }}</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            @for (member of members(); track member.id) {
              <tr>
                <td>
                  @if (member.photoUrl) {
                    <img [src]="member.photoUrl" class="row-photo" alt="" />
                  } @else {
                    <span class="row-photo row-photo-placeholder">{{ member.name.charAt(0) }}</span>
                  }
                </td>
                <td class="title-cell">{{ member.name }}</td>
                <td>{{ member.roleEn }}</td>
                <td>{{ member.sortOrder }}</td>
                <td>{{ (member.slug ? 'admin.team.yes' : 'admin.team.no') | transloco }}</td>
                <td class="actions">
                  <button class="btn-link" type="button" (click)="startEdit(member)">
                    {{ 'admin.events.edit' | transloco }}
                  </button>
                  <button class="btn-link danger" type="button" (click)="remove(member)">
                    {{ 'admin.events.delete' | transloco }}
                  </button>
                </td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <app-empty-state [message]="'admin.team.empty' | transloco" />
    }
  `,
  styles: `
    .team-form {
      padding: 1.6rem 1.8rem 1.8rem;
      max-width: 900px;
      background: #fff;
      margin-bottom: 1.5rem;
    }

    .lang-block {
      border: 1px solid var(--ad-border, #e4dcc9);
      border-radius: var(--ad-radius, 10px);
      padding: 0.9rem 1rem 1rem;
      background: #fdfcf9;
      margin: 1rem 0;
    }

    .bio-hint {
      margin: 0.6rem 0 0;
      font-size: 0.82rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .grid-2 {
      display: grid;
      gap: 1rem;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
    }

    .field {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
      margin-bottom: 0.9rem;

      label {
        font-weight: 600;
        font-size: 0.85rem;
        color: var(--yb-brown);
      }

      input,
      textarea {
        font: inherit;
        padding: 0.55rem 0.8rem;
        border: 1.5px solid var(--ad-border-strong, #d3c7ac);
        border-radius: 8px;
      }
    }

    .cover-field {
      align-items: flex-start;
    }

    .image-preview {
      max-height: 120px;
      width: auto;
      border-radius: 8px;
      margin-bottom: 0.5rem;
    }

    .uploading-note {
      font-size: 0.85rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .form-actions {
      display: flex;
      gap: 1rem;
      margin-top: 1rem;
    }

    .row-photo {
      width: 36px;
      height: 36px;
      border-radius: 50%;
      object-fit: cover;
      display: block;
    }

    .row-photo-placeholder {
      display: flex;
      align-items: center;
      justify-content: center;
      background: var(--yb-brown);
      color: var(--yb-cream);
      font-family: var(--yb-font-heading);
    }

    .title-cell {
      font-weight: 600;
    }

    .actions {
      white-space: nowrap;
      display: flex;
      gap: 0.7rem;
    }
  `
})
export class AdminTeam {
  private api = inject(ApiService);
  private confirm = inject(ConfirmService);
  private toasts = inject(ToastService);
  private transloco = inject(TranslocoService);
  private fb = inject(FormBuilder);

  readonly members = signal<TeamMemberDto[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly contentLang = signal<Lang>('en');
  readonly editing = signal<number | 'new' | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    roleEn: [''],
    roleNl: [''],
    roleAr: [''],
    photoUrl: [null as string | null],
    bioEn: [null as string | null],
    bioNl: [null as string | null],
    bioAr: [null as string | null],
    sortOrder: [0]
  });

  constructor() {
    this.load();
  }

  load() {
    this.loading.set(true);
    this.api.adminGetTeam().subscribe({
      next: members => {
        this.members.set(members);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  startNew() {
    this.form.reset({
      name: '', roleEn: '', roleNl: '', roleAr: '',
      photoUrl: null, bioEn: null, bioNl: null, bioAr: null,
      sortOrder: this.members().length
    });
    this.editing.set('new');
  }

  startEdit(member: TeamMemberDto) {
    this.form.reset({
      name: member.name,
      roleEn: member.roleEn, roleNl: member.roleNl, roleAr: member.roleAr,
      photoUrl: member.photoUrl,
      bioEn: member.bioEn, bioNl: member.bioNl, bioAr: member.bioAr,
      sortOrder: member.sortOrder
    });
    this.editing.set(member.id);
  }

  cancelEdit() {
    this.editing.set(null);
  }

  upload(input: Event) {
    const target = input.target as HTMLInputElement;
    const file = target.files?.[0];
    target.value = '';
    if (!file) return;

    this.uploading.set(true);
    this.api.adminUpload(file).subscribe({
      next: result => {
        this.form.controls.photoUrl.setValue(result.url);
        this.uploading.set(false);
      },
      error: () => {
        this.uploading.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  clearPhoto() {
    this.form.controls.photoUrl.setValue(null);
  }

  save() {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const value = this.form.getRawValue() as TeamMemberInput;
    const editing = this.editing();
    const request = editing === 'new'
      ? this.api.adminCreateTeamMember(value)
      : this.api.adminUpdateTeamMember(editing as number, value);

    request.subscribe({
      next: () => {
        this.saving.set(false);
        this.editing.set(null);
        this.toasts.success(this.transloco.translate('admin.common.saved'));
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.toasts.error(this.transloco.translate('admin.media.genericError'));
      }
    });
  }

  async remove(member: TeamMemberDto) {
    const ok = await this.confirm.ask({
      message: this.transloco.translate('admin.team.confirmDelete'),
      confirmLabel: this.transloco.translate('admin.events.delete'),
      cancelLabel: this.transloco.translate('admin.media.cancel'),
      danger: true
    });
    if (!ok) return;

    this.api.adminDeleteTeamMember(member.id).subscribe({
      next: () => {
        this.toasts.success(this.transloco.translate('admin.media.deleted'));
        this.load();
      },
      error: () => this.toasts.error(this.transloco.translate('admin.media.genericError'))
    });
  }
}
