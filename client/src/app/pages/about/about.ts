import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { CmsPipe } from '../../core/cms.pipe';
import { LanguageService } from '../../core/language.service';
import { TeamMemberDto } from '../../core/models';
import { Reveal } from '../../shared/motion/reveal.directive';
import { Tilt } from '../../shared/motion/tilt.directive';

@Component({
  selector: 'app-about',
  imports: [CmsPipe, RouterLink, Reveal, Tilt],
  templateUrl: './about.html',
  styleUrl: './about.scss'
})
export class About {
  private api = inject(ApiService);
  readonly lang = inject(LanguageService);

  readonly aboutImage = toSignal(
    this.api.getSettings().pipe(map(s => s['aboutImageUrl'] || null)),
    { initialValue: null }
  );

  readonly team = toSignal(this.api.getTeam(), { initialValue: [] as TeamMemberDto[] });

  readonly values = [
    { key: 'Pride', color: 'var(--yb-red)' },
    { key: 'Openness', color: 'var(--yb-blue)' },
    { key: 'Service', color: 'var(--yb-green)' },
    { key: 'Excellence', color: 'var(--yb-orange)' },
    { key: 'Unity', color: 'var(--yb-yellow)' }
  ];
}
