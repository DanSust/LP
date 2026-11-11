// UserStatusService.ts
import { Injectable, signal, computed } from '@angular/core';
import { SignalRConnectionManager } from './SignalRConnectionManager';

interface UserStatus {
  userId: string;
  isOnline: boolean;
  lastSeen?: Date;
}

@Injectable({ providedIn: 'root' })
export class UserStatusService {
  public userId = '';
  public onlineUsers = signal<Set<string>>(new Set());
  public userStatuses = signal<Map<string, UserStatus>>(new Map());
  public onlineCount = computed(() => this.onlineUsers().size);

  private handlersRegistered = false;
  private connectPromise: Promise<void> | null = null;
  private onlineUsersPromise: Promise<void> | null = null;

  constructor(private connectionManager: SignalRConnectionManager) {
    this.connectionManager.onDisconnect(() => {
      this.clearState();
    });

    // Список онлайн-пользователей нужно обновлять после каждого подключения.
    this.connectionManager.onConnected(() => {
      void this.requestOnlineUsers();
    });
  }

  /**
   * Инициализация статусов. Вызывается один раз после авторизации.
   * Повторные вызовы безопасны и не создают новые подключения.
   */
  connect(userId: string): Promise<void> {
    if (this.connectPromise) {
      return this.connectPromise;
    }

    this.connectPromise = this.connectInternal(userId).finally(() => {
      this.connectPromise = null;
    });

    return this.connectPromise;
  }

  private async connectInternal(userId: string): Promise<void> {
    await this.connectionManager.ensureConnection();
    this.registerHandlersOnce();
    this.userId = this.connectionManager.userId ?? userId;
    await this.requestOnlineUsers();
  }

  private registerHandlersOnce(): void {
    if (this.handlersRegistered) {
      return;
    }

    this.connectionManager.on('userStatusChanged', (data: { userId: string; status: boolean }) => {
      if (!data?.userId) {
        return;
      }
      this.updateUserStatus(data.userId, data.status);
    });

    this.connectionManager.on('OnlineUsersList', (userIds: string[]) => {
      this.setOnlineUsers(userIds);
    });

    this.handlersRegistered = true;
  }

  async getUserStatus(userId: string): Promise<boolean> {
    try {
      return await this.connectionManager.invoke('GetUserStatus', userId);
    } catch {
      return false;
    }
  }

  async getOnlineUsers(): Promise<string[]> {
    try {
      const users = await this.connectionManager.invoke('GetOnlineUsers');
      return Array.isArray(users) ? users.map(String) : [];
    } catch {
      return [];
    }
  }

  requestOnlineUsers(): Promise<void> {
    if (this.onlineUsersPromise) {
      return this.onlineUsersPromise;
    }

    this.onlineUsersPromise = this.getOnlineUsers()
      .then(users => this.setOnlineUsers(users))
      .finally(() => {
        this.onlineUsersPromise = null;
      });

    return this.onlineUsersPromise;
  }

  isUserOnline(userId: string): boolean {
    return this.onlineUsers().has(userId);
  }

  getUserPresence(userId: string): UserStatus | undefined {
    return this.userStatuses().get(userId);
  }

  reset(): void {
    this.userId = '';
    this.connectPromise = null;
    this.onlineUsersPromise = null;
    this.clearState();
  }

  private updateUserStatus(userId: string, isOnline: boolean): void {
    const statuses = new Map(this.userStatuses());

    if (isOnline) {
      this.onlineUsers.update(users => new Set(users).add(userId));
      statuses.set(userId, { userId, isOnline: true });
    } else {
      this.onlineUsers.update(users => {
        const newSet = new Set(users);
        newSet.delete(userId);
        return newSet;
      });
      statuses.set(userId, { userId, isOnline: false, lastSeen: new Date() });
    }

    this.userStatuses.set(statuses);
  }

  private setOnlineUsers(userIds: string[]): void {
    const safeUserIds = Array.isArray(userIds) ? userIds.map(String) : [];
    this.onlineUsers.set(new Set(safeUserIds));

    const statuses = new Map<string, UserStatus>();
    safeUserIds.forEach(id => statuses.set(id, { userId: id, isOnline: true }));
    this.userStatuses.set(statuses);
  }

  private clearState(): void {
    this.onlineUsers.set(new Set());
    this.userStatuses.set(new Map());
  }
}
