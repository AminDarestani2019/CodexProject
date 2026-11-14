import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', title: 'SecureLogin | Home', loadComponent: () => import('./home/home').then((module) => module.Home) },
  { path: '**', redirectTo: '' },
];
