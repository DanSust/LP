import { AfterViewInit, Component, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { AuthService } from './../services/AuthService';
import { TelegrmaComponent } from './Telegram/telegram.component';
import { Router } from '@angular/router';

declare global {
  interface Window {
    VKIDSDK?: any;
  }
}

@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [
    CommonModule,
    TelegrmaComponent,
    ReactiveFormsModule // <-- Add this line
  ],
  //template: `<div *ngIf="visible">Hi</div>`,
  templateUrl: './auth.component.html',
  styleUrls: ['./auth.component.scss']
})
export class AuthComponent implements AfterViewInit, OnDestroy {
  public loginForm: FormGroup;
  public registerForm: FormGroup;
  public activeTab: 'login' | 'register' = 'login';
  public isLoading = false;
  public registerErrorMessage: string | null = null;
  get acceptTerms() { return this.registerForm.get('acceptTerms'); }


  private readonly authTelUrl = `${window.location.origin}/api/auth/telegram`;
  private vkInitialized = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    // Создаем формы в конструкторе - это гарантирует их инициализацию
    this.loginForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]]
    });

    this.registerForm = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', Validators.required],
      acceptTerms: [false, Validators.requiredTrue]
    });
  }

  ngAfterViewInit() {
    if (!this.vkInitialized) {
      this.initVK();
    }
  }

  ngOnDestroy() {
    // Очистка при уничтожении компонента
    this.vkInitialized = false;
  }

  private initVK() {
    if (!window.VKIDSDK) {
      console.log('VK SDK not loaded yet, retrying...');
      setTimeout(() => this.initVK(), 100);
      return;
    }

    // Проверяем, не инициализирован ли уже виджет
    const container = document.getElementById('vk-id-container');
    if (!container || container.children.length > 0) {
      return; // Уже есть содержимое
    }

    console.log('VK SDK loaded, initializing widget...');
    const VKID = window.VKIDSDK;

    try {
      VKID.Config.init({
        app: 54434160,
        redirectUrl: window.location.origin,
        responseMode: VKID.ConfigResponseMode.Callback,
        source: VKID.ConfigSource.LOWCODE,
        scope: '',
      });

      const oAuth = new VKID.OAuthList();

      oAuth.render({
        container: document.getElementById('vk-id-container'),
        oauthList: ['vkid', 'mail_ru', 'ok_ru'],
        styles: {
          borderRadius: 12,
        }
      })
        .on(VKID.WidgetEvents.ERROR, (error: any) => {
          console.error('VK ID Error:', error);
        })
        .on(VKID.OAuthListInternalEvents.LOGIN_SUCCESS, (payload: any) => {
          const code = payload.code;
          const deviceId = payload.device_id;

          VKID.Auth.exchangeCode(code, deviceId)
            .then((data: any) => {
              console.log('VK auth success:', data);
              this.handleVKLogin(data);
            })
            .catch((error: any) => {
              console.error('VK exchange error:', error);
            });
        });

      console.log('VK ID widget rendered');
    } catch (e) {
      console.error('VK init error:', e);
    }
  }

  private handleVKLogin(data: any) {
    // Обработка успешного входа через VK
    console.log('Handle VK login:', data);
    this.authService.socialLogin('vk', data);
  }

  switchTab(tab: 'login' | 'register'): void {
    this.activeTab = tab;
  }

  onLogin(): void {
    if (this.loginForm.valid) {
      this.isLoading = true;
      const { email, password } = this.loginForm.value;
      this.authService.login(email, password).subscribe({
        next: () => {
          this.isLoading = false;
          console.log('Успешный вход');
          this.router.navigate(['/profile']);
        },
        error: (error) => {
          this.isLoading = false;
          console.error('Ошибка входа:', error);
        }
      });
    }
  }

  onRegister(): void {
    if (this.registerForm.valid) {
      this.isLoading = true;
      const { email, password } = this.registerForm.value;
      this.authService.register(email, password).subscribe({
        next: (response: any) => {
          this.isLoading = false;
          if (response.Success) {
            // Успех
            this.switchTab('login');
          } else {
            // Показываем ошибку в вашем формате
            this.registerErrorMessage = response.message;
          }
        },
        error: (error) => {
          this.isLoading = false;
          console.error('Ошибка регистрации:', error);
        }
      });
    }
  }

  GoogleLogin(event: any): void {
    event.preventDefault();
    const popup = window.open(
      'https://127.0.0.1:7010/api/oauth/google/login?' + window,
      'OAuthPopup',
      'width=600,height=700'
    );
  }

  // Telegram Login Widget
  telegramLogin(): void {
    // Генерируем уникальный state для защиты от CSRF
    const state = this.generateCSRFToken();
    sessionStorage.setItem('telegram_auth_state', state);

    // Формируем URL для Telegram OAuth
    const params = new URLSearchParams({
      bot_id: '8220602308',
      origin: window.location.origin,
      request_access: 'write',
      auth_url: this.authTelUrl,
      state: state
    });

    // Открываем в отдельном окне (как у Google)
    const popup = window.open(
      `https://oauth.telegram.org/auth?${params.toString()}`,
      'TelegramAuth',
      'width=600,height=700,scrollbars=yes'
    );

    // Проверяем закрытие popup
    const checkClosed = setInterval(() => {
      if (popup?.closed) {
        clearInterval(checkClosed);
        this.isLoading = false;
      }
    }, 1000);
  }

  private generateCSRFToken(): string {
    return Array.from(crypto.getRandomValues(new Uint8Array(32)))
      .map(b => b.toString(16).padStart(2, '0'))
      .join('');
  }

  // Геттеры для удобного доступа к контролам
  get loginEmail() { return this.loginForm.get('email'); }
  get loginPassword() { return this.loginForm.get('password'); }
  get registerName() { return this.registerForm.get('name'); }
  get registerEmail() { return this.registerForm.get('email'); }
  get registerPassword() { return this.registerForm.get('password'); }
  get confirmPassword() { return this.registerForm.get('confirmPassword'); }
}
