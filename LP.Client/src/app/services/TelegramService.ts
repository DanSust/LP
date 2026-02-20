import { HttpClient } from "@angular/common/http";
import { Inject, inject, Injectable } from "@angular/core";
import { TelegramUser } from "../Interfaces/TelegramUser";
import { API_BASE_URL } from "../app.config";
import { Router } from "@angular/router";
import { AuthService } from "../services/AuthService";


// telegram-auth.service.ts
@Injectable({ providedIn: 'root' })
export class TelegramAuthService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);

  constructor(@Inject(API_BASE_URL) private baseUrl: string,
    private router: Router)
{ }

  // Загружаем скрипт виджета динамически
  loadScript(container: HTMLElement): Promise<void> {
    return new Promise((resolve, reject) => {
      // Удаляем старый скрипт если есть
      const oldScript = document.getElementById('telegram-widget-script');
      if (oldScript) oldScript.remove();

      const script = document.createElement('script');
      script.id = 'telegram-widget-script';
      script.src = 'https://telegram.org/js/telegram-widget.js?22';
      script.setAttribute('data-telegram-login', 'PulseMatchBot_bot');
      script.setAttribute('data-size', 'large');
      script.setAttribute('data-radius', '8');
      script.setAttribute('data-request-access', 'write');
      script.setAttribute('data-userpic', 'false');
      script.async = true;

      // Глобальный callback
      (window as any).onTelegramAuth = (user: TelegramUser) => this.handleAuth(user);
      script.setAttribute('data-onauth', 'onTelegramAuth(user)');

      script.onload = () => {
        console.log('Telegram script loaded');
        resolve();
      };
      script.onerror = (e) => {
        console.error('Telegram script failed', e);
        reject(e);
      };

      // ВСТАВЛЯЕМ В КОНТЕЙНЕР, а не в body!
      container.appendChild(script);
      console.log('Script appended to container', container);
    });
  }

  private handleAuth(user: TelegramUser): void {
    const authData = {
      Id: user.id,
      FirstName: user.first_name,
      LastName: user.last_name,
      Username: user.username,
      PhotoUrl: user.photo_url,
      AuthDate: user.auth_date,
      Hash: user.hash
    };

    console.log(this.baseUrl + '/api/auth/telegram/verify', authData);
    this.http.post(this.baseUrl + '/api/auth/telegram/verify', authData, { headers: { 'Content-Type': 'application/json' }, withCredentials: true }).subscribe({
      next: (response: any) => {
        //localStorage.setItem('token', response.token);
        // Редирект или обновление состояния        
        this.authService.handleAuth(response, response.userId);        
      },
      error: (err) => console.error('Auth failed', err)
    });
  }
}

