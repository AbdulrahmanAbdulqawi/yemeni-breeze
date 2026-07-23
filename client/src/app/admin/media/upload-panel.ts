import { Component, computed, inject, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { ApiService } from '../../core/api.service';
import { MediaType } from '../../core/models';

type UploadState = 'queued' | 'uploading' | 'done' | 'failed';

interface UploadTask {
  id: number;
  file: File;
  state: UploadState;
  percent: number;
}

const MAX_CONCURRENT = 3;

@Component({
  selector: 'app-upload-panel',
  imports: [TranslocoPipe],
  template: `
    <div
      class="dropzone"
      [class.dragover]="dragging()"
      (dragover)="$event.preventDefault(); dragging.set(true)"
      (dragleave)="dragging.set(false)"
      (drop)="onDrop($event)">
      <p class="dz-title">{{ 'admin.media.dropTitle' | transloco }}</p>
      <p class="dz-sub">
        {{ 'admin.media.dropOr' | transloco }}
        <input type="file" accept="image/*,video/*,application/pdf" multiple (change)="onPick($event)" />
      </p>
      <p class="dz-target">
        {{ 'admin.media.uploadingTo' | transloco }}: <strong>{{ targetName() }}</strong>
      </p>
    </div>

    @if (tasks().length) {
      <div class="queue card">
        <div class="queue-head">
          <span>{{ doneCount() }} / {{ tasks().length }}</span>
          @if (!busy()) {
            <button type="button" class="btn-link" (click)="clearFinished()">
              {{ 'admin.media.clearFinished' | transloco }}
            </button>
          }
        </div>
        @for (task of tasks(); track task.id) {
          <div class="queue-row" [class]="task.state">
            <span class="q-name" [title]="task.file.name">{{ task.file.name }}</span>
            <span class="q-bar">
              <span class="q-fill" [style.width.%]="task.percent"></span>
            </span>
            <span class="q-state">
              @switch (task.state) {
                @case ('queued') { {{ 'admin.media.queued' | transloco }} }
                @case ('uploading') { {{ task.percent }}% }
                @case ('done') { ✓ }
                @case ('failed') {
                  <button type="button" class="btn-link danger" (click)="retry(task)">
                    {{ 'admin.media.retry' | transloco }}
                  </button>
                }
              }
            </span>
          </div>
        }
      </div>
    }
  `,
  styles: `
    .dropzone {
      border: 2px dashed var(--yb-blue);
      border-radius: var(--ad-radius, 10px);
      background: #fff;
      padding: 1.6rem;
      text-align: center;
      transition: background 0.15s ease;

      &.dragover {
        background: #eef;
        border-style: solid;
      }
    }

    .dz-title {
      font-weight: 700;
      font-size: 1rem;
      margin: 0 0 0.3rem;
    }

    .dz-sub {
      margin: 0;
      font-size: 0.9rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .dz-target {
      margin: 0.7rem 0 0;
      font-size: 0.85rem;
      color: var(--ad-muted, #7c6a5c);
    }

    .queue {
      margin-top: 0.9rem;
      padding: 0.7rem 0.9rem;
      background: #fff;
    }

    .queue-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.85rem;
      color: var(--ad-muted, #7c6a5c);
      margin-bottom: 0.4rem;
    }

    .queue-row {
      display: grid;
      grid-template-columns: minmax(0, 1fr) 110px 62px;
      gap: 0.7rem;
      align-items: center;
      padding: 0.28rem 0;
      font-size: 0.85rem;

      &.failed .q-name {
        color: var(--yb-red);
      }
    }

    .q-name {
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    .q-bar {
      height: 6px;
      background: var(--ad-bg, #f4f1ea);
      border-radius: 999px;
      overflow: hidden;
    }

    .q-fill {
      display: block;
      height: 100%;
      background: var(--yb-blue);
      transition: width 0.15s ease;
    }

    .done .q-fill {
      background: #1d7c2e;
    }

    .failed .q-fill {
      background: var(--yb-red);
    }

    .q-state {
      text-align: end;
      font-variant-numeric: tabular-nums;
      color: var(--ad-muted, #7c6a5c);
    }
  `
})
export class UploadPanel {
  private api = inject(ApiService);

  readonly targetName = input.required<string>();
  readonly uploaded = output<{ url: string; thumbUrl: string | null; mediaType: MediaType }>();
  readonly batchFinished = output<{ ok: number; failed: number }>();

  readonly dragging = signal(false);
  readonly tasks = signal<UploadTask[]>([]);

  readonly doneCount = computed(() => this.tasks().filter(t => t.state === 'done').length);
  readonly busy = computed(() => this.tasks().some(t => t.state === 'queued' || t.state === 'uploading'));

  private nextId = 1;
  private active = 0;
  private batch = { ok: 0, failed: 0 };

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.dragging.set(false);
    this.enqueue(Array.from(event.dataTransfer?.files ?? []));
  }

  onPick(event: Event) {
    const input = event.target as HTMLInputElement;
    this.enqueue(Array.from(input.files ?? []));
    input.value = '';
  }

  private enqueue(files: File[]) {
    if (!files.length) return;
    this.tasks.update(list => [
      ...list,
      ...files.map(file => ({ id: this.nextId++, file, state: 'queued' as UploadState, percent: 0 }))
    ]);
    this.pump();
  }

  retry(task: UploadTask) {
    this.patch(task.id, { state: 'queued', percent: 0 });
    this.pump();
  }

  clearFinished() {
    this.tasks.update(list => list.filter(t => t.state !== 'done'));
  }

  /** Starts as many queued uploads as the concurrency budget allows. */
  private pump() {
    while (this.active < MAX_CONCURRENT) {
      const next = this.tasks().find(t => t.state === 'queued');
      if (!next) break;
      this.start(next);
    }
  }

  private start(task: UploadTask) {
    this.active++;
    this.patch(task.id, { state: 'uploading', percent: 0 });

    this.api.adminUploadWithProgress(task.file).subscribe({
      next: progress => {
        if (progress.done && progress.result) {
          this.patch(task.id, { state: 'done', percent: 100 });
          this.uploaded.emit(progress.result);
        } else if (!progress.done) {
          this.patch(task.id, { percent: progress.percent });
        }
      },
      error: () => {
        this.patch(task.id, { state: 'failed' });
        this.batch.failed++;
        this.finishOne();
      },
      complete: () => {
        this.batch.ok++;
        this.finishOne();
      }
    });
  }

  private finishOne() {
    this.active--;
    this.pump();
    if (!this.busy()) {
      this.batchFinished.emit({ ...this.batch });
      this.batch = { ok: 0, failed: 0 };
    }
  }

  private patch(id: number, changes: Partial<UploadTask>) {
    this.tasks.update(list => list.map(t => (t.id === id ? { ...t, ...changes } : t)));
  }
}
