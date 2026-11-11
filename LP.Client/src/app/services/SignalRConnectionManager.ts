import { Inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  HttpTransportType,
  LogLevel
} from '@microsoft/signalr';
import { firstValueFrom, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { API_BASE_URL, API_HUB_URL } from '../app.config';

type DisconnectCallback = () => void;
type ReconnectedCallback = () => void | Promise<void>;

interface AuthStatus {
  isAuthenticated?: boolean;
  userId?: string | null;
}

@Injectable({ providedIn: 'root' })
export class SignalRConnectionManager {
  private hubConnection: HubConnection | null = null;
  private connectionPromise: Promise<void> | null = null;
  private authPromise: Promise<boolean> | null = null;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private manualDisconnect = false;

  private disconnectCallbacks = new Set<DisconnectCallback>();
  private reconnectedCallbacks = new Set<ReconnectedCallback>();
  private connectedCallbacks = new Set<ReconnectedCallback>();
  private handlers = new Map<string, (...args: any[]) => void>();
  private attachedHandlers = new Set<string>();

  public userId: string | null = null;

  public isConnected = signal(false);
  public isConnecting = signal(false);
  public isReconnecting = signal(false);
  public reconnectAttempts = signal(0);
  public lastError = signal<string | null>(null);

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string,
    @Inject(API_HUB_URL) private hub: string
  ) {
    // Подключение запускается явно после загрузки пользователя (см. App.ngOnInit).
    // Автозапуск в конструкторе создавал гонку с /auth/status.
  }

  /**
   * Единственная точка входа для подключения.
   * Пока идёт подключение, все вызывающие получают один и тот же промис.
   */
  public ensureConnection(): Promise<void> {
    if (this.hubConnection?.state === HubConnectionState.Connected) {
      this.isConnected.set(true);
      return Promise.resolve();
    }

    if (this.connectionPromise) {
      return this.connectionPromise;
    }

    this.connectionPromise = this.connectInternal().finally(() => {
      this.connectionPromise = null;
    });

    return this.connectionPromise;
  }

  private async connectInternal(): Promise<void> {
    this.manualDisconnect = false;
    this.isConnecting.set(true);
    this.lastError.set(null);

    try {
      const authenticated = await this.checkAuth();
      if (!authenticated || !this.userId) {
        throw new Error('User not authenticated');
      }

      if (this.hubConnection?.state === HubConnectionState.Connected) {
        this.isConnected.set(true);
        return;
      }

      await this.createConnection();
    } catch (error) {
      this.isConnected.set(false);
      this.lastError.set(error instanceof Error ? error.message : String(error));
      throw error;
    } finally {
      this.isConnecting.set(false);
    }
  }

  /**
   * Проверяет авторизацию один раз. Повторные вызовы переиспользуют результат,
   * пока userId не сброшен через reset().
   */
  private checkAuth(): Promise<boolean> {
    if (this.userId) {
      return Promise.resolve(true);
    }

    if (!this.authPromise) {
      this.authPromise = firstValueFrom(
        this.http
          .get<AuthStatus>(`${this.base}/auth/status`, { withCredentials: true })
          .pipe(
            map(result => {
              this.userId = result.userId ?? null;
              return result.isAuthenticated === true && !!result.userId;
            }),
            catchError(() => of(false))
          )
      ).finally(() => {
        this.authPromise = null;
      });
    }

    return this.authPromise;
  }

  private async createConnection(): Promise<void> {
    if (this.hubConnection) {
      await this.stopConnection();
    }

    const hubUrl = `${this.hub.replace(/\/$/, '')}/chat`;
    // ChatHub берёт пользователя из query, если cookies/claims недоступны.
    const fullUrl = this.userId
      ? `${hubUrl}?userId=${encodeURIComponent(this.userId)}`
      : hubUrl;

    const connection = new HubConnectionBuilder()
      .withUrl(fullUrl, {
        withCredentials: true,
        skipNegotiation: true,
        transport: HttpTransportType.WebSockets
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: retryContext => {
          const attempt = retryContext.previousRetryCount + 1;
          this.reconnectAttempts.set(attempt);
          this.isReconnecting.set(true);
          return Math.min(2000 * Math.pow(2, attempt - 1), 30000);
        }
      })
      .configureLogging(LogLevel.Warning)
      .build();

    this.hubConnection = connection;
    this.attachedHandlers.clear();

    connection.onreconnecting(() => {
      if (this.hubConnection !== connection) {
        return;
      }
      this.isConnected.set(false);
      this.isReconnecting.set(true);
    });

    connection.onreconnected(() => {
      if (this.hubConnection !== connection) {
        return;
      }
      this.isConnected.set(true);
      this.isReconnecting.set(false);
      this.reconnectAttempts.set(0);
      void this.notifyReconnected();
      void this.notifyConnected();
    });

    connection.onclose(() => {
      // Старое соединение могло закрыться уже после создания нового.
      if (this.hubConnection !== connection) {
        return;
      }

      this.isConnected.set(false);
      this.isReconnecting.set(false);
      this.reconnectAttempts.set(0);
      this.hubConnection = null;
      this.attachedHandlers.clear();
      this.notifyDisconnect();

      if (!this.manualDisconnect) {
        this.scheduleReconnect();
      }
    });

    this.attachHandlers(connection);

    try {
      await connection.start();
      this.isConnected.set(true);
      this.isReconnecting.set(false);
      this.reconnectAttempts.set(0);
      this.lastError.set(null);
      void this.notifyConnected();
    } catch (error) {
      this.hubConnection = null;
      this.attachedHandlers.clear();
      this.isConnected.set(false);

      try {
        await connection.stop();
      } catch {
        // Соединение уже могло быть закрыто.
      }

      if (!this.manualDisconnect) {
        this.scheduleReconnect();
      }

      throw error;
    }
  }

  private attachHandlers(connection: HubConnection): void {
    for (const [eventName, callback] of this.handlers) {
      connection.on(eventName, callback);
      this.attachedHandlers.add(eventName);
    }
  }

  private scheduleReconnect(): void {
    if (this.reconnectTimer || this.manualDisconnect) {
      return;
    }

    const attempt = this.reconnectAttempts() + 1;
    this.reconnectAttempts.set(attempt);

    const delay = Math.min(5000 * attempt, 30000);
    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;

      if (this.manualDisconnect) {
        return;
      }

      this.ensureConnection().catch(() => this.scheduleReconnect());
    }, delay);
  }

  private clearReconnectTimer(): void {
    if (this.reconnectTimer) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }
  }

  private async stopConnection(): Promise<void> {
    const connection = this.hubConnection;
    this.hubConnection = null;
    this.attachedHandlers.clear();

    if (connection) {
      try {
        await connection.stop();
      } catch {
        // Игнорируем ошибку остановки уже закрытого соединения.
      }
    }
  }

  /**
   * Регистрирует обработчик один раз. При повторной регистрации заменяет старый,
   * чтобы не плодить дубли после переподключений.
   */
  public on(eventName: string, callback: (...args: any[]) => void): void {
    const previous = this.handlers.get(eventName);
    this.handlers.set(eventName, callback);

    if (!this.hubConnection) {
      return;
    }

    if (previous && previous !== callback) {
      this.hubConnection.off(eventName, previous);
      this.hubConnection.on(eventName, callback);
      this.attachedHandlers.add(eventName);
      return;
    }

    if (!this.attachedHandlers.has(eventName)) {
      this.hubConnection.on(eventName, callback);
      this.attachedHandlers.add(eventName);
    }
  }

  public async invoke(methodName: string, ...args: any[]): Promise<any> {
    await this.ensureConnection();

    if (!this.hubConnection || this.hubConnection.state !== HubConnectionState.Connected) {
      throw new Error('SignalR is not connected');
    }

    return this.hubConnection.invoke(methodName, ...args);
  }

  public async disconnect(): Promise<void> {
    this.manualDisconnect = true;
    this.clearReconnectTimer();
    this.isConnected.set(false);
    this.isReconnecting.set(false);
    this.reconnectAttempts.set(0);

    await this.stopConnection();
    this.notifyDisconnect();
  }

  /**
   * Полный сброс при выходе из аккаунта: следующий вход заново проверит /auth/status.
   */
  public async reset(): Promise<void> {
    await this.disconnect();
    this.userId = null;
    this.authPromise = null;
    this.lastError.set(null);
  }

  public onDisconnect(callback: DisconnectCallback): void {
    this.disconnectCallbacks.add(callback);
  }

  public offDisconnect(callback: DisconnectCallback): void {
    this.disconnectCallbacks.delete(callback);
  }

  public onReconnected(callback: ReconnectedCallback): void {
    this.reconnectedCallbacks.add(callback);
  }

  public offReconnected(callback: ReconnectedCallback): void {
    this.reconnectedCallbacks.delete(callback);
  }

  /**
   * Вызывается после каждого успешного установления соединения:
   * и при первом подключении, и после переподключения.
   */
  public onConnected(callback: ReconnectedCallback): void {
    this.connectedCallbacks.add(callback);
  }

  public offConnected(callback: ReconnectedCallback): void {
    this.connectedCallbacks.delete(callback);
  }

  private notifyDisconnect(): void {
    this.disconnectCallbacks.forEach(callback => callback());
  }

  private async notifyReconnected(): Promise<void> {
    for (const callback of this.reconnectedCallbacks) {
      try {
        await callback();
      } catch {
        // Один упавший обработчик не должен ломать остальные.
      }
    }
  }

  private async notifyConnected(): Promise<void> {
    for (const callback of this.connectedCallbacks) {
      try {
        await callback();
      } catch {
        // Один упавший обработчик не должен ломать остальные.
      }
    }
  }
}
