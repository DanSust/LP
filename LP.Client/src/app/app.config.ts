import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection, InjectionToken, inject } from '@angular/core';
import { CanActivateFn, provideRouter, Router, Routes, withExperimentalAutoCleanupInjectors } from '@angular/router';
import { routes } from './app.routes';
import { provideHttpClient, withFetch } from '@angular/common/http';
import localeRu from '@angular/common/locales/ru';
import { LOCALE_ID } from '@angular/core';
import * as moment from './DateAdapter';
import { CustomDateAdapter } from './DateAdapter';
import { DateAdapter, MAT_DATE_LOCALE } from '@angular/material/core';
import { registerLocaleData } from '@angular/common';

import { AboutComponent } from "./common/about.component";
import { NotFoundComponent } from "./not-found.component";
import { UserLocationComponent } from "./user/location/user-location";
import { UserProfile } from './user/profile/user-profile';
import { UserView } from './user/view/user-view';
import { ChatView } from './chat/chat-component';
import { ChatLayoutComponent } from './chat/chat-layout.component';
import { ChatAboutComponent } from './chat/chat-about.component';
import { ChatListComponent } from './chat/chat-list.component';
import { AuthCallbackComponent } from './auth/auth.callback';
import { AuthComponent } from './auth/auth.component';
import { VoteComponent } from './vote/vote.component';
import { MatchComponent } from './match/match.component';
import { SearchComponent } from './search/search.component';
import { EventsViewComponent } from './events/events-view.component';
import { AuthGuard } from './auth/auth.guard';
import { AuthService } from './services/AuthService';
import { ConfirmComponent } from './auth/auth.confirm.component';
import { ComingSoonComponent } from './promo/coming-soon.component';

//import { FotoScrollComponent } from './common/foto-scroll';

const appRoutes: Routes = [
  { path: "about", component: AboutComponent, canActivate: [AuthGuard] },  
  { path: "location", component: UserLocationComponent },
  { path: "soon", component: ComingSoonComponent },
  { path: "profile", component: UserProfile, canActivate: [AuthGuard] },
  { path: "vote", component: VoteComponent, canActivate: [AuthGuard] },
  { path: "vote/:id", component: VoteComponent, canActivate: [AuthGuard] },
  //{ path: "chat/:id", component: ChatView, canActivate: [AuthGuard] },
  { path: 'chat',
    component: ChatLayoutComponent,
    canActivate: [AuthGuard],
    children: [
      { path: '', component: ChatAboutComponent },
      { path: ':id', component: ChatView }
    ]
  },
  { path: "authcallback", component: AuthCallbackComponent },  
  { path: "match", component: MatchComponent, canActivate: [AuthGuard] },
  { path: "search", component: SearchComponent, canActivate: [AuthGuard] },
  { path: "events", component: EventsViewComponent, canActivate: [AuthGuard] },
  { path: "auth", component: AuthComponent },
  { path: "confirm", component: ConfirmComponent },
  /*{ path: "home", component: SoPulseComponent },*/
  { path: "logout",
    canActivate: [() => {
      inject(AuthService).logout();
      return false; //inject(Router).createUrlTree(['/auth']); // Возвращаем UrlTree
    }],
    component: AboutComponent // Любой компонент, guard перенаправит
  },
  { path: '',
    loadComponent: () => import('./promo/so-pulse.component')
      .then(m => m.SoPulseComponent),
    canMatch: [() => !inject(AuthService).isAuthenticated()]
  },
  {
    path: '',
    loadComponent: () => import('./common/about.component')
      .then(m => m.AboutComponent),
    canMatch: [() => inject(AuthService).isAuthenticated()]
  },
  /*{ path: "**", component: NotFoundComponent },*/
  
  //{ path: "foto", component: FotoScrollComponent }
];

export function getBaseUrl() {
  //console.log("getBaseUrl");
  //return "https://localhost:7010";
  return "https://127.0.0.1:7010";
}

export function getDefaultAvatarUrl() {
  const baseUrl = inject(API_BASE_URL);
  return `${baseUrl}/assets/default-avatar.svg`;
}

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
export const DEFAULT_AVATAR_URL = new InjectionToken<string>('DEFAULT_AVATAR_URL');
registerLocaleData(localeRu);

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withFetch()),
    { provide: API_BASE_URL, useFactory: getBaseUrl },
    { provide: DEFAULT_AVATAR_URL, useFactory: getDefaultAvatarUrl },
    { provide: LOCALE_ID, useValue: 'ru-RU' },   // ← DD.MM.YYYY
    { provide: DateAdapter, useClass: CustomDateAdapter },
    { provide: MAT_DATE_LOCALE, useValue: 'ru-RU' },
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),    
    provideRouter(appRoutes, withExperimentalAutoCleanupInjectors())
  ]
};
