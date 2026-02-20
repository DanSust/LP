// src/app/services/signalr-connection-manager.ts
import { Inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Observable } from 'rxjs/internal/Observable';
import { API_BASE_URL } from '../app.config';
import { catchError, map, of, firstValueFrom } from 'rxjs';

type DisconnectCallback = () => void;

@Injectable({ providedIn: 'root' })
export class SignalRConnectionManager {
  private readonly HUB_URL = 'https://127.0.0.1:5000/chat';

  private hubConnection: HubConnection | null = null;
  public userId: string | null = null;
  private connectionPromise: Promise<void> | null = null;
  private disconnectCallbacks = new Set<DisconnectCallback>();

  // ✅ Публичные сигналы состояния
  public isConnected = signal(false);
  public reconnectAttempts = signal(0);
  public isReconnecting = signal(false);

  private handlers = new Map<string, (...args: any[]) => void>(); // ✅ ХРАНИМ ОБРАБОТЧИКИ

  constructor(private http: HttpClient, @Inject(API_BASE_URL) private base: string) {
    // 🚀 Автоматическое подключение при создании сервиса
    this.ensureConnection();
  }

  /**
   * ✅ ЕДИНСТВЕННЫЙ способ получить подключение
   * Автоматически вызывается в конструкторе
   * @returns Promise, который резолвится когда подключение готово
   */
  public ensureConnection(): Promise<void> {
    if (this.hubConnection?.state === 'Connected') {
      console.log('✅ Already connected');
      return Promise.resolve();
    }

    if (this.connectionPromise) {
      console.log('⏳ Connection in progress...');
      return this.connectionPromise;
    }

    // Получаем userId и создаем подключение
    this.connectionPromise = firstValueFrom(this.checkAuth()).then(isAuthenticated => {
      if (!isAuthenticated || !this.userId) {
        throw new Error('User not authenticated');
      }
      return this.createConnection();
    });

    return this.connectionPromise;
  }

  /**
   * Проверяем аутентификацию и получаем userId
   */
  private checkAuth(): Observable<boolean> {
    return this.http.get<any>(`${this.base}/api/auth/status`, { withCredentials: true }).pipe(
      map(result => {
        this.userId = result.userId;
        return result.isAuthenticated;
      }),
      catchError(err => {
        console.error('✗ Auth error:', err);
        return of(false);
      })
    );
  }

  private async createConnection(): Promise<void> {
    const fullUrl = `${this.HUB_URL}?userId=${this.userId}`;
    console.log('🔗 Connecting:', fullUrl);

    this.hubConnection = new HubConnectionBuilder()
      .withUrl(fullUrl, { skipNegotiation: true, transport: 1, withCredentials: true })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          const delay = Math.min(2000 * Math.pow(2, retryContext.previousRetryCount), 30000);
          this.reconnectAttempts.set(retryContext.previousRetryCount + 1);
          this.isReconnecting.set(true);
          return delay + Math.random() * 5000;
        }
      })
      .configureLogging(LogLevel.Information)
      .build();

    this.hubConnection.onreconnecting(() => {
      console.warn('🔌 Reconnecting...');
      this.isConnected.set(false);
    });

    this.hubConnection.onreconnected(() => {
      console.log('✅ Reconnected');
      this.isReconnecting.set(false);
      this.reconnectAttempts.set(0);
      this.isConnected.set(true);
    });

    this.hubConnection.onclose(() => {
      console.error('🔴 Connection closed');
      this.isConnected.set(false);
      this.isReconnecting.set(false);
      this.reconnectAttempts.set(0);
      this.notifyDisconnect();
      this.hubConnection = null;
      this.connectionPromise = null;

      // ✅ Авто-переподключение через 5 секунд
      setTimeout(() => this.ensureConnection(), 5000);
    });

    this.handlers.forEach((callback, eventName) => {
      this.hubConnection?.on(eventName, callback);
    });

    await this.hubConnection.start();
    this.isConnected.set(true);
    this.isReconnecting.set(false);
    console.log('✅ Connected');
  }

  public on(eventName: string, callback: (...args: any[]) => void): void {
    this.handlers.set(eventName, callback);
    this.hubConnection?.on(eventName, callback);
  }

  public invoke(methodName: string, ...args: any[]): Promise<any> {
    if (!this.hubConnection || this.hubConnection.state !== 'Connected') {
      return Promise.reject('Not connected');
    }
    return this.hubConnection.invoke(methodName, ...args);
  }

  public disconnect(): void {
    console.log('🔴 Manual disconnect');
    this.hubConnection?.stop();
  }

  public onDisconnect(callback: DisconnectCallback): void {
    this.disconnectCallbacks.add(callback);
  }

  public offDisconnect(callback: DisconnectCallback): void {
    this.disconnectCallbacks.delete(callback);
  }

  private notifyDisconnect(): void {
    this.disconnectCallbacks.forEach(callback => callback());
  }
}
