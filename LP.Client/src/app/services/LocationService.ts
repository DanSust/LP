// location.service.ts
import { Injectable, signal } from '@angular/core';
import { Observable, from } from 'rxjs';

export interface LocationCoords {
  latitude: number;
  longitude: number;
  accuracy: number;
  timestamp: number;
}

interface PositionErrorLike {
  code: number;
  message?: string;
}

@Injectable({
  providedIn: 'root'
})
export class LocationService {
  private position = signal<LocationCoords | null>(null);
  private error = signal<string | null>(null);
  private isLoading = signal(false);

  readonly position$ = this.position.asReadonly();
  readonly error$ = this.error.asReadonly();
  readonly isLoading$ = this.isLoading.asReadonly();

  // Быстрый запрос по сети/кэшу. Если он не сработал и разрешение уже выдано —
  // пробуем watchPosition с GPS и бо́льшим таймаутом.
  getCurrentPosition(): Observable<LocationCoords> {
    return from(this.requestPosition());
  }

  private async requestPosition(): Promise<LocationCoords> {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      if (!('geolocation' in navigator)) {
        throw { code: -1 } as PositionErrorLike;
      }

      let coords: LocationCoords;

      try {
        coords = await this.readPosition({
          enableHighAccuracy: false,
          timeout: 10000,
          maximumAge: 5 * 60 * 1000
        });
      } catch (firstError) {
        const permission = await this.readPermissionState();

        if (permission === 'denied') {
          throw { code: 1 } as PositionErrorLike;
        }

        // Если диалог разрешения ещё открыт, повторный запрос ничего не изменит.
        if (permission === 'prompt') {
          throw { code: 4 } as PositionErrorLike;
        }

        coords = await this.watchFirstPosition({
          enableHighAccuracy: true,
          timeout: 25000,
          maximumAge: 0
        });
      }

      this.position.set(coords);
      this.error.set(null);
      return coords;
    } catch (error) {
      const message = this.handleError(error as PositionErrorLike);
      this.error.set(message);
      throw message;
    } finally {
      this.isLoading.set(false);
    }
  }

  private readPosition(options: PositionOptions): Promise<LocationCoords> {
    return new Promise((resolve, reject) => {
      navigator.geolocation.getCurrentPosition(
        position => resolve(this.toCoords(position)),
        error => reject(error),
        options
      );
    });
  }

  private watchFirstPosition(options: PositionOptions): Promise<LocationCoords> {
    return new Promise((resolve, reject) => {
      let settled = false;
      let watchId = 0;
      let timer: ReturnType<typeof setTimeout> | null = null;

      const finish = (callback: () => void): void => {
        if (settled) return;
        settled = true;
        if (timer !== null) clearTimeout(timer);
        try {
          navigator.geolocation.clearWatch(watchId);
        } catch {
          // watchId мог быть не инициализирован — не критично.
        }
        callback();
      };

      try {
        watchId = navigator.geolocation.watchPosition(
          position => finish(() => resolve(this.toCoords(position))),
          error => finish(() => reject(error)),
          options
        );
      } catch (error) {
        settled = true;
        reject(error);
        return;
      }

      // Не все браузеры соблюдают timeout у watchPosition, поэтому свой таймер.
      if (options.timeout && options.timeout > 0) {
        timer = setTimeout(() => {
          finish(() => reject({ code: 3 } as PositionErrorLike));
        }, options.timeout);
      }
    });
  }

  private async readPermissionState(): Promise<PermissionState | null> {
    if (!navigator.permissions?.query) return null;

    try {
      const result = await navigator.permissions.query({
        name: 'geolocation' as PermissionName
      });
      return result.state;
    } catch {
      return null;
    }
  }

  private toCoords(position: GeolocationPosition): LocationCoords {
    return {
      latitude: position.coords.latitude,
      longitude: position.coords.longitude,
      accuracy: position.coords.accuracy,
      timestamp: position.timestamp
    };
  }

  private handleError(error: PositionErrorLike): string {
    switch (error.code) {
      case -1:
        return 'Геолокация не поддерживается этим браузером';
      case 1:
        return 'Доступ к геолокации запрещён. Разрешите его в настройках браузера.';
      case 2:
        return 'Не удалось определить местоположение. Включите Wi‑Fi/мобильную сеть или выберите город вручную.';
      case 3:
        return 'Местоположение не определено вовремя. Выберите город вручную или нажмите «Повторить».';
      case 4:
        return 'Подтвердите доступ к геолокации в окне браузера, затем нажмите «Повторить».';
      default:
        return 'Ошибка определения местоположения. Выберите город вручную.';
    }
  }

  // ✅ Watch position changes
  watchPosition(): Observable<LocationCoords> {
    return new Observable(observer => {
      if (!('geolocation' in navigator)) {
        observer.error('Геолокация не поддерживается этим браузером');
        return;
      }

      const watchId = navigator.geolocation.watchPosition(
        position => {
          const coords = this.toCoords(position);
          this.position.set(coords);
          observer.next(coords);
        },
        error => observer.error(this.handleError(error)),
        { enableHighAccuracy: true, maximumAge: 60000 }
      );

      return () => navigator.geolocation.clearWatch(watchId);
    });
  }
}
