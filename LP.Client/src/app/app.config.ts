import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideZoneChangeDetection, InjectionToken, inject } from '@angular/core';
import { CanActivateFn, provideRouter, Router, Routes, withExperimentalAutoCleanupInjectors } from '@angular/router';
import { provideHttpClient, withFetch } from '@angular/common/http';
import localeRu from '@angular/common/locales/ru';
import { LOCALE_ID } from '@angular/core';
import * as moment from './DateAdapter';
import { CustomDateAdapter } from './DateAdapter';
import { DateAdapter, MAT_DATE_LOCALE } from '@angular/material/core';
import { registerLocaleData } from '@angular/common';

import { AboutComponent } from "./common/about.component";
import { AuthGuard } from './auth/auth.guard';
import { AuthService } from './services/AuthService';
import { environment } from '../environments/environment';

const appRoutes: Routes = [
  {
    path: "about",
    component: AboutComponent,
    data: {
      indexable: true,
      canonical: '/about',
      seo: {
        title: 'О сервисе Made4Love — знакомства для серьёзных отношений',
        description: 'Узнайте, как работает Made4Love: проверка совместимости, живой чат, безопасное общение и сообщество людей, которые ищут серьёзные отношения.'
      }
    }
  },
  { path: "location", loadComponent: () => import('./user/location/user-location').then(m => m.UserLocationComponent) },
  { path: "soon", loadComponent: () => import('./promo/coming-soon.component').then(m => m.ComingSoonComponent) },
  { path: "profile", loadComponent: () => import('./user/profile/user-profile').then(m => m.UserProfile), canActivate: [AuthGuard] },
  { path: "scroll", loadComponent: () => import('./foto/foto-scroll-component').then(m => m.FotoScrollComponent) }, 
  { path: "vote", loadComponent: () => import('./vote/vote.component').then(m => m.VoteComponent) },
  { path: "vote/:id", loadComponent: () => import('./vote/vote.component').then(m => m.VoteComponent) },
  //{ path: "chat/:id", component: ChatView, canActivate: [AuthGuard] },
  { path: 'chat',
    loadComponent: () => import('./chat/chat-layout.component').then(m => m.ChatLayoutComponent),
    canActivate: [AuthGuard],
    children: [
      { path: '', loadComponent: () => import('./chat/chat-about.component').then(m => m.ChatAboutComponent) },
      { path: ':id', loadComponent: () => import('./chat/chat-component').then(m => m.ChatView) }
    ]
  },
  { path: "authcallback", loadComponent: () => import('./auth/auth.callback').then(m => m.AuthCallbackComponent) },
  { path: "match", loadComponent: () => import('./match/match.component').then(m => m.MatchComponent), canActivate: [AuthGuard] },
  { path: "search", loadComponent: () => import('./search/search.component').then(m => m.SearchComponent) },
  {
    path: "events",
    loadComponent: () => import('./events/events-view.component').then(m => m.EventsViewComponent),
    data: {
      indexable: true,
      canonical: '/events',
      dynamicSeo: true,
      seo: {
        title: 'События и знакомства — Made4Love',
        description: 'События и встречи Made4Love: узнавайте о новых мероприятиях сервиса и находите людей, с которыми интересно провести время.'
      }
    }
  },
  {
    path: "events/:slug",
    loadComponent: () => import('./events/event-detail.component').then(m => m.EventDetailComponent),
    data: {
      indexable: true,
      dynamicSeo: true
    }
  },
  {
    path: "journal",
    loadComponent: () => import('./journal/journal-view.component').then(m => m.JournalViewComponent),
    data: {
      indexable: true,
      canonical: '/journal',
      dynamicSeo: true,
      seo: {
        title: 'Журнал Made4Love — отношения, города и события',
        description: 'Журнал Made4Love: разборы, городские подборки и материалы о знакомствах и отношениях. Редакция проверяет каждый текст и указывает источник.'
      }
    }
  },
  {
    path: "journal/:slug",
    loadComponent: () => import('./journal/journal-detail.component').then(m => m.JournalDetailComponent),
    data: {
      indexable: true,
      dynamicSeo: true
    }
  },
  {
    path: "admin/events",
    loadComponent: () => import('./admin/admin-events.component').then(m => m.AdminEventsComponent),
    data: {
      indexable: false,
      seo: {
        title: 'Админка событий — Made4Love',
        description: 'Управление источниками и модерация событий Made4Love.'
      }
    }
  },
  {
    path: "admin/journal",
    loadComponent: () => import('./admin/admin-journal.component').then(m => m.AdminJournalComponent),
    data: {
      indexable: false,
      seo: {
        title: 'Админка журнала — Made4Love',
        description: 'Редактура и модерация материалов журнала Made4Love.'
      }
    }
  },
  {
    path: "admin",
    pathMatch: "full",
    redirectTo: "admin/events"
  },
  { path: "auth", loadComponent: () => import('./auth/auth.component').then(m => m.AuthComponent) },
  { path: "confirm", loadComponent: () => import('./auth/auth.confirm.component').then(m => m.ConfirmComponent) },
  { path: "reset-password", loadComponent: () => import('./auth/reset-password.component').then(m => m.ResetPasswordComponent) },
  /*{ path: "home", component: SoPulseComponent },*/
  { path: "logout",
    canActivate: [() => {
      inject(AuthService).logout();
      return false; //inject(Router).createUrlTree(['/auth']); // Возвращаем UrlTree
    }],
    component: AboutComponent // Любой компонент, guard перенаправит
  },
  {
    path: "",
    pathMatch: "full",
    component: AboutComponent,
    data: {
      indexable: true,
      canonical: '/',
      seo: {
        title: 'Made4Love — знакомства для серьёзных отношений',
        description: 'Made4Love — знакомства для серьёзных отношений. Проверка совместимости, живой чат и денежный приз за свадьбу. Создайте анкету бесплатно.'
      }
    }
  },
  //{ path: "",
  //  loadComponent: () => import('./promo/so-pulse.component')
  //    .then(m => m.SoPulseComponent),
  //  canMatch: [() => !inject(AuthService).isAuthenticated()]
  //},
  //{
  //  path: '',
  //  loadComponent: () => import('./common/about.component')
  //    .then(m => m.AboutComponent),
  //  canMatch: [() => !inject(AuthService).isAuthenticated()]
  //},
  {
    path: "**",
    loadComponent: () => import('./not-found.component').then(m => m.NotFoundComponent),
    data: {
      indexable: false,
      seo: {
        title: 'Страница не найдена — Made4Love',
        description: 'Запрошенная страница не найдена. Вернитесь на главную Made4Love или посмотрите другие разделы сервиса.'
      }
    }
  },

  //{ path: "foto", component: FotoScrollComponent }
];

export function getBaseUrl() {
  console.log("getBaseUrl - ", environment.apiUrl);
  //return '';
  //return "https://127.0.0.1:7010";
  //return "https://localhost:7010";
  return environment.apiUrl;
}

export function getBaseHub() {
  console.log("getBaseHub - ", environment.hubUrl);  
  return environment.hubUrl;
}

export function getBaseJournalUrl() {
  return environment.journalApiUrl;
}

export function getEnvironment(): string {
  return environment.production ? 'production' : 'development';
}

export function getDefaultAvatarUrl() {
  const baseUrl = inject(API_BASE_URL);
  return `${baseUrl}/assets/default-avatar.svg`;
}

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');
export const API_HUB_URL = new InjectionToken<string>('API_HUB_URL');
export const JOURNAL_API_BASE_URL = new InjectionToken<string>('JOURNAL_API_BASE_URL');
export const APP_ENVIRONMENT = new InjectionToken<string>('APP_ENVIRONMENT');
export const DEFAULT_AVATAR_URL = new InjectionToken<string>('DEFAULT_AVATAR_URL');
registerLocaleData(localeRu);

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(withFetch()),
    { provide: API_BASE_URL, useFactory: getBaseUrl },
    { provide: API_HUB_URL, useFactory: getBaseHub },
    { provide: JOURNAL_API_BASE_URL, useFactory: getBaseJournalUrl },
    //{ provide: API_BASE_URL, useValue: environment.apiUrl },
    //{ provide: API_HUB_URL, useValue: environment.hubUrl },
    { provide: APP_ENVIRONMENT, useFactory: getEnvironment },
    { provide: DEFAULT_AVATAR_URL, useFactory: getDefaultAvatarUrl },
    { provide: LOCALE_ID, useValue: 'ru-RU' },   // ← DD.MM.YYYY
    { provide: DateAdapter, useClass: CustomDateAdapter },
    { provide: MAT_DATE_LOCALE, useValue: 'ru-RU' },
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),    
    provideRouter(appRoutes, withExperimentalAutoCleanupInjectors())
  ]
};
