import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { map } from 'rxjs';
import { ApiService } from '../../core/api.service';

@Component({
  selector: 'app-about',
  imports: [TranslocoPipe, RouterLink],
  templateUrl: './about.html',
  styleUrl: './about.scss'
})
export class About {
  private api = inject(ApiService);

  readonly aboutImage = toSignal(
    this.api.getSettings().pipe(map(s => s['aboutImageUrl'] || null)),
    { initialValue: null }
  );

  readonly team = [
    { name: 'Koutaiba', roleKey: 'about.roleFounder' },
    { name: 'Ali', roleKey: 'about.roleCoFounder' },
    { name: 'Nawaf', roleKey: 'about.roleBrand' },
    { name: 'Belal', roleKey: 'about.roleFood' },
    { name: 'Abdulrahman', roleKey: 'about.roleProgram' }
  ];

  readonly values = [
    { key: 'Pride', color: 'var(--yb-red)' },
    { key: 'Openness', color: 'var(--yb-blue)' },
    { key: 'Service', color: 'var(--yb-green)' },
    { key: 'Excellence', color: 'var(--yb-orange)' },
    { key: 'Unity', color: 'var(--yb-yellow)' }
  ];
}
