import { Component, computed, effect, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { catchError, map, of, switchMap } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { LanguageService } from '../../core/language.service';
import { TeamMemberDto } from '../../core/models';
import { SeoService } from '../../core/seo.service';

interface MemberLookup {
  loading: boolean;
  member: TeamMemberDto | null;
}

@Component({
  selector: 'app-team-member-page',
  imports: [TranslocoPipe, RouterLink],
  templateUrl: './team-member-page.html',
  styleUrl: './team-member-page.scss'
})
export class TeamMemberPage {
  private api = inject(ApiService);
  private router = inject(Router);
  private seo = inject(SeoService);
  readonly lang = inject(LanguageService);

  readonly slug = input.required<string>();

  private readonly lookup = toSignal(
    toObservable(this.slug).pipe(
      switchMap(slug => this.api.getTeamMember(slug).pipe(
        map((member): MemberLookup => ({ loading: false, member })),
        catchError(() => of<MemberLookup>({ loading: false, member: null }))
      ))
    ),
    { initialValue: { loading: true, member: null } as MemberLookup }
  );

  readonly member = computed(() => this.lookup().member);

  private redirectIfMissing = effect(() => {
    const lookup = this.lookup();
    if (!lookup.loading && lookup.member === null) this.router.navigate(['/about']);
  });

  private seoSync = effect(() => {
    const member = this.member();
    this.lang.current();
    if (member) this.seo.setTags(member.name, this.lang.pick(member, 'role'));
  });
}
