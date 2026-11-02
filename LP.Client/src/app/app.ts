import { Component, CUSTOM_ELEMENTS_SCHEMA, Inject, inject, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ReactiveFormsModule } from '@angular/forms';
import { FotoScrollComponent } from './foto/foto-scroll-component';
import { BottomNavComponent } from './common/bottom-nav.component';
import { BackCollageComponent } from './common/background-collage.component';
import { AuthService } from './services/AuthService';
import { AuthComponent } from './auth/auth.component';
import { UserStatusService } from './services/UserStatusService';
import { ChatService } from './services/ChatService';
import { NavigationService } from './services/NavigationService';
import { filter, Subscription } from 'rxjs';
import { routes } from './app.routes';
import { HttpClient } from "@angular/common/http";
import { API_BASE_URL } from './app.config';
import { ComingSoonComponent } from './promo/coming-soon.component';
import { SeoConfig, SeoService } from './services/SeoService';


@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterOutlet,
    RouterModule,
    //UserProfile,
    BottomNavComponent,
    FotoScrollComponent,
    BackCollageComponent,
    ComingSoonComponent
  ],
  //schemas: [CUSTOM_ELEMENTS_SCHEMA], // ✅ Add this
  templateUrl: './app.html',
  styleUrl: './app.scss'
})

export class App implements OnInit, OnDestroy {
  protected title = 'tmp-standalone';
  caption = 'made4love';
  userId: string | null = null;
  isLoading = false;
  isMobile = false;

  // === НАСТРОЙКИ РЕЖИМА РАЗРАБОТКИ ===
  isDevelopmentMode = false; //localStorage.getItem('is_admin') !== 'true'
  launchDate = new Date(2026, 2, 16); // 8 марта 2026
  // ===================================

  private channel = new BroadcastChannel('auth-channel');
  private subscription?: Subscription;
  private routeSeoSubscription?: Subscription;

  userStatusService = inject(UserStatusService);
  chatService = inject(ChatService);

  private router = inject(Router);
  private seoService = inject(SeoService);
  get isHomeRoute(): boolean {
    return this.router.url === '/' || this.router.url === '/auth';
  }

  constructor(
    private authService: AuthService,
    private navService: NavigationService,
    private http: HttpClient,
    @Inject(API_BASE_URL) private baseURL: string
  ) { }

  ngOnInit() {
    this.updateSeoForCurrentRoute();
    this.routeSeoSubscription = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe(() => this.updateSeoForCurrentRoute());

    if (this.isDevelopmentMode) {
      return;
    }

    this.isMobile = window.innerWidth <= 768 || /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent);

    this.channel.addEventListener('message', (event) => {
      if (event.data.type === 'oauth-done') {
        const { code, userId } = event.data.payload;
        console.warn(event.data);
        //setTimeout(function () {
        //  console.log("Этот текст появится через 1 секунду!");
        //  window.location.href = "/profile";
        //}, 300000);
        //this.router.navigate(['/']);

        this.authService.handleAuth(code, userId);
      }
    });
    
    // Load user ID on app startup
    this.authService.loadUserId();    

    // Subscribe to userId changes
    this.subscription = this.authService.userId$.subscribe(userId => {
      if (userId)
        this.userId = userId;
      console.log('User ID loaded:', userId);
      // setTimeout(() => { this.isLoading = false }, 1000, "Alice");
      this.chatService.loadChats();
    });
  }

  logout(): void {
    this.authService.logout();    
  }

  ngOnDestroy(): void {
    this.subscription?.unsubscribe();
    this.routeSeoSubscription?.unsubscribe();
    this.channel.close();
  }

  private updateSeoForCurrentRoute(): void {
    let route = this.router.routerState.snapshot.root;
    while (route.firstChild) route = route.firstChild;

    const path = this.router.url.split(/[?#]/, 1)[0] || '/';
    const canonicalPath = route.data['canonical'] as string | undefined;
    const seo = route.data['seo'] as SeoConfig | undefined;

    this.seoService.updateMetaTags({
      ...seo,
      url: canonicalPath || path,
      robots: route.data['indexable'] === true ? 'index,follow' : 'noindex,nofollow'
    });
  }
}
