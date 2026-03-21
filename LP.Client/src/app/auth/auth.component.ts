import { AfterViewInit, Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule } from '@angular/forms';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { AuthService } from './../services/AuthService';
import { TelegrmaComponent } from './Telegram/telegram.component';
import { Router } from '@angular/router';
import { API_BASE_URL } from '../app.config';
import { ToastService } from '../common/toast.service';

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
export class AuthComponent implements AfterViewInit, OnInit, OnDestroy {
  public loginForm: FormGroup;
  public registerForm: FormGroup;
  public activeTab: 'login' | 'register' = 'login';
  public isLoading = false;
  public registerErrorMessage: string | null = null;
  get acceptTerms() { return this.registerForm.get('acceptTerms'); }


  private readonly authTelUrl = `${window.location.origin}/api/auth/telegram/verify`;
  private vkInitialized = false;
  private vkScriptId = 'vk-sdk-script';

  constructor(
    @Inject(API_BASE_URL) private baseUrl: string,
    private fb: FormBuilder,
    private authService: AuthService,
    private toast: ToastService,
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

  ngOnInit(): void {
    this.loadVKScript();
    if (window.location.hash.includes('tgAuthResult=')) {
      this.checkTelegramHash();
    }
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

  private loadVKScript() {
    // 1. Если SDK уже в объекте window, просто инициализируем
    if (window.VKIDSDK) {
      this.initVK();
      return;
    }

    // 2. Если скрипт уже есть в DOM (например, от прошлого визита), 
    // но SDK еще не готов, ждем загрузки
    const existingScript = document.getElementById(this.vkScriptId) as HTMLScriptElement;
    if (existingScript) {
      existingScript.onload = () => this.initVK();
      return;
    }

    // 3. Создаем и добавляем скрипт
    const script = document.createElement('script');
    script.id = this.vkScriptId;
    script.src = 'https://unpkg.com/@vkid/sdk@<3.0.0/dist-sdk/umd/index.js';
    script.async = true;
    script.onload = () => {
      console.log('VK SDK loaded');
      this.initVK();
    };
    script.onerror = () => console.error('VK SDK load failed');

    document.head.appendChild(script);
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
        scope: 'vkid.personal_info email', // <--- Обязательно добавь это!
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

          console.log('OAuthListInternalEvents.LOGIN_SUCCESS - ', payload);

          VKID.Auth.exchangeCode(code, deviceId)
            .then(async (data: any) => {
              console.log('VK tokens received:', data);

              let extendedData = { ...data };

              try {
                // Пробуем получить данные пользователя через более стабильный метод
                // В SDK 2.x данные часто можно забрать через Auth.userInfo(accessToken)
                const userInfo = await VKID.Auth.userInfo(data.access_token);
                console.log('User info from Auth:', userInfo);

                extendedData.firstName = userInfo.first_name || userInfo.user?.first_name;
                extendedData.lastName = userInfo.last_name || userInfo.user?.last_name;
                extendedData.avatarUrl = userInfo.avatar || userInfo.user?.avatar;
              } catch (userError) {
                console.warn('Failed to get user info via Auth.userInfo:', userError);
              }

              this.handleVKLogin(extendedData);
            })
            .catch((error: any) => console.error('VK exchange error:', error));
        });

      console.log('VK ID widget rendered');
    } catch (e) {
      console.error('VK init error:', e);
    }
  }

  private handleVKLogin(data: any) {
    // Обработка успешного входа через VK
    console.log('Handle VK login:', data);

    // Отправляем полученный id_token или access_token на бэкенд
    this.authService.verifyVkToken(data).subscribe({
      next: (res) => {
        this.isLoading = false;
        localStorage.setItem('auth_token', res.token);
        localStorage.setItem('userId', res.user_id);
        //this.router.navigate(['/profile']);
        window.location.href = '/profile';
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Ошибка верификации VK:', err);
      }
    });

    //this.authService.socialLogin('vk', data).subscribe;

    //this.authService.socialLogin('vk', data).subscribe({
    //  next: (res) => {
    //    this.isLoading = false;
    //    this.router.navigate(['/profile']);
    //  },
    //  error: (err) => {
    //    this.isLoading = false;
    //    console.error(err);
    //  }
    //});
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
          setTimeout(() => this.router.navigate(['/profile']), 500);          
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
            this.toast.error(response.message);
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
    console.log(this.baseUrl + '/oauth/google/login?');
    event.preventDefault();
    const popup = window.open(
      this.baseUrl+'/oauth/google/login?' + window,
      'OAuthPopup',
      'width=600,height=700'
    );
  }

  // Telegram Login Widget
  telegramLogin(): void {
    console.log('telegramLogin()');
    // Генерируем уникальный state для защиты от CSRF
    const state = this.generateCSRFToken();
    sessionStorage.setItem('telegram_auth_state', state);

    // Формируем URL для Telegram OAuth
    const params = new URLSearchParams({
      auth_url: this.authTelUrl,
      bot_id: '8220602308',
      origin: window.location.origin,
      request_access: 'write',      
      embed: '1',
      state: state
    });

    //console.log('https://oauth.telegram.org/auth?', params.toString());

    // Открываем в отдельном окне (как у Google)
    const tgURL = `https://oauth.telegram.org/auth?${params.toString()}`;
    console.log(tgURL);
    const popup = window.open(tgURL,
      'TelegramAuth',
      'width=600,height=700,scrollbars=yes'
    );

    if (!popup) {
      console.error("Popup заблокирован браузером!");
      alert("Разрешите всплывающие окна для этого сайта");
    } else {
      console.log("Popup открыт, url:", tgURL);
    }

    // Подписываемся на событие ответа от окна
    const handleMessage = (event: MessageEvent) => {
      // Проверка безопасности: только с вашего домена
      if (event.origin !== window.location.origin) return;

      if (event.data.type === 'TG_AUTH_SUCCESS') {
        const authResponse = event.data.data;
        console.log('Данные получены из попапа:', authResponse);

        // Вызываем ваш метод завершения авторизации (сохранение токена и т.д.)
        this.authService.handleAuth(authResponse, authResponse.userId);

        window.removeEventListener('message', handleMessage);
      }

      if (event.data.type === 'TG_AUTH_ERROR') {
        this.toast.error('Ошибка авторизации через Telegram');
        window.removeEventListener('message', handleMessage);
      }
    };

    window.addEventListener('message', handleMessage);

    // Проверяем закрытие popup
    //const checkClosed = setInterval(() => {
    //  if (popup?.closed) {
    //    clearInterval(checkClosed);
    //    this.isLoading = false;
    //  }
    //}, 1000);
  }

  private checkTelegramHash(val: string = '') {
    console.log('checkTelegramHash called, hash:', window.location.hash);
    console.log('window.opener:', window.opener);

    const hash = window.location.hash;
    if (hash.includes('tgAuthResult=')) {
      console.log('Found tgAuthResult in hash');
      const base64Data = hash.split('tgAuthResult=')[1];
      try {
        const userData = JSON.parse(atob(base64Data));
        console.log('Parsed userData:', userData);

        if (window.opener) {
          console.log('Sending message to opener and closing');
          window.opener.postMessage({ type: 'TG_AUTH_SUCCESS', payload: userData }, window.location.origin);
          window.close();
        } else {
          console.log('No opener, handling auth directly');
          this.authService.handleAuth(userData, userData.id);
        }
      } catch (e) {
        console.error('Ошибка парсинга TG данных', e);
      }
    } else {
      console.log('No tgAuthResult in hash');
    }
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
