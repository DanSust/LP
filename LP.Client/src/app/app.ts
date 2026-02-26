import { Component, CUSTOM_ELEMENTS_SCHEMA, Inject, inject, OnDestroy, OnInit } from '@angular/core';
import { Router, RouterOutlet } from '@angular/router';
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
import { Subscription } from 'rxjs';
import { routes } from './app.routes';
import { HttpClient } from "@angular/common/http";
import { API_BASE_URL } from './app.config';
import { ComingSoonComponent } from './promo/coming-soon.component';


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
  isLoading = true;

  // === НАСТРОЙКИ РЕЖИМА РАЗРАБОТКИ ===
  isDevelopmentMode = true; // ← Поставьте false чтобы включить основной сайт
  launchDate = new Date(2026, 2, 8); // 8 марта 2026
  // ===================================

  private channel = new BroadcastChannel('auth-channel');
  private subscription?: Subscription;

  userStatusService = inject(UserStatusService);
  chatService = inject(ChatService);
  constructor(private authService: AuthService,
    private http: HttpClient,
    @Inject(API_BASE_URL) private baseURL: string
  ) { }

  ngOnInit() {
    if (this.isDevelopmentMode) {
      return;
    }

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
    this.channel.close();
  }
}
