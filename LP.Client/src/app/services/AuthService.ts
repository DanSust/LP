// auth.service.ts
import { Injectable, Inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of, delay, tap } from 'rxjs';
import { API_BASE_URL } from './../app.config';
import { Router } from '@angular/router';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private userIdSubject = new BehaviorSubject<string | null>(null);
  userId$ = this.userIdSubject.asObservable();
  apiUrl: any;

  constructor(
    private http: HttpClient,
    private router: Router,
    @Inject(API_BASE_URL) private baseUrl: string
  ) { }

  isAuthenticated(): boolean {
    // Проверяем localStorage/token/session
    const userId = localStorage.getItem('userId');

    console.log('isAuthenticated' + userId);
    return userId != null;
    // ИЛИ return !!this.userSubject.value;
  }

  // Get user ID from server
  getUserId(): Observable<any> {
    return this.http.get<any>(this.baseUrl + '/api/auth/status', { withCredentials: true });
  }

  // Load and cache user ID
  loadUserId(): void {
    const cachedUserId = localStorage.getItem('userId');
    if (cachedUserId) {
      this.userIdSubject.next(cachedUserId); // ← Мгновенно показываем старый ID
    }

    this.getUserId().subscribe({
      next: (result) => {
        console.log(result);
        const userId = result.userId || result.id || result.username;
        this.userIdSubject.next(userId);
        if (userId)
          localStorage.setItem('userId', userId); // Optional: cache in localStorage
      },
      error: (error) => {
        console.error('Failed to load user ID:', error);
        this.userIdSubject.next(null);
      }
    });
  }

  // Get cached user ID
  getCachedUserId(): string | null {
    return this.userIdSubject.value || localStorage.getItem('userId');
  }

  login(email: string, password: string): Observable<any> {
    //return of({ success: true }).pipe(delay(1000));
    return this.http.post<any>(this.baseUrl + '/api/auth/login', { username: email, password }, { withCredentials: true })
      .pipe(tap(response => {
        const userId = response.userId || response.id;
        if (userId) {
          localStorage.setItem('userId', userId);
          this.userIdSubject.next(userId);
        }
        this.router.navigate(['/profile']);
      }
      ));
  }

  logout(): void {
    console.log('=== LOGOUT START ===');
    console.log('Cookies before logout:', document.cookie);
    console.log('localStorage userId:', localStorage.getItem('userId'));
    this.http.get<any>(this.baseUrl + '/api/auth/logout', { withCredentials: true })
      .subscribe(() => {
        localStorage.removeItem('userId');        
        this.userIdSubject.next(null);
        document.cookie = "auth=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/";
        document.cookie = "UserId=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/";
        window.location.href = '/auth';
      });    
  }

  handleAuth(code: any, userId: any) {    
    //localStorage.setItem('code', code);
    localStorage.setItem('userId', userId);
    this.userIdSubject.next(userId);
    window.location.href = '/profile';
    //this.router.navigate(['/profile']);
  }

  register(email: string, password: string): Observable<{ Success: boolean; message: string; userId?: string }> {
    return this.http.post<{ Success: boolean; message: string; userId?: string }>(this.baseUrl + '/api/auth/register', {
      username: email,
      password
    });
  }

  confirmEmail(token: string, email: string) {
    this.http.get(this.baseUrl + `/confirm?token=${token}&email=${email}`).subscribe(
      () => this.router.navigate(['/profile'])
    );

  }

  socialLogin(provider: 'vk' | 'mailru', data: any): Observable<any> {    
    return new Observable(observer => {
      this.http.get<{ url: string }>(this.baseUrl + '/auth' + provider).subscribe({
        next: (response) => {
          console.log('socialLogin ', provider);
          const popup = window.open(
            response.url,
            'VKAuth',
            'width=600,height=700,scrollbars=yes,resizable=yes'
          );

          if (!popup) {
            observer.error('Блокировщик всплывающих окон. Разрешите всплывающие окна.');
            return;
          }

          const messageHandler = (event: MessageEvent) => {
            if (event.origin !== window.location.origin) return;

            if (event.data.type === 'VK_AUTH_SUCCESS') {
              window.removeEventListener('message', messageHandler);             
              localStorage.setItem('auth_token', event.data.token);
              observer.next(event.data);
              observer.complete();
            } else if (event.data.type === 'VK_AUTH_ERROR') {
              window.removeEventListener('message', messageHandler);
              observer.error(event.data.error);
            }
          };

          window.addEventListener('message', messageHandler);

          const checkClosed = setInterval(() => {
            if (popup.closed) {
              clearInterval(checkClosed);
              window.removeEventListener('message', messageHandler);
              observer.error('Окно авторизации закрыто');
            }
          }, 1000);
        },
        error: (err) => observer.error(err)
      });
    });
  }

  forgotPassword(email: string): Observable<void> {
    console.log('Forgot password:', email);
    return of(undefined).pipe(delay(500));
  }
}
