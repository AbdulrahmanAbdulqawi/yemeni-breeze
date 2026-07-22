import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-about',
  imports: [TranslocoPipe, RouterLink],
  templateUrl: './about.html',
  styleUrl: './about.scss'
})
export class About {
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
