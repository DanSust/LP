// location.service.ts
import { Injectable, signal } from '@angular/core';
import { Observable, from } from 'rxjs';

export interface LocationCoords {
  latitude: number;
  longitude: number;
  accuracy: number;
  timestamp: number;
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

  // ✅ Request location with RxJs Observable
  getCurrentPosition(): Observable<LocationCoords> {
    return from(
      new Promise<LocationCoords>((resolve, reject) => {
        this.isLoading.set(true);
        this.error.set(null);

        if (!('geolocation' in navigator)) {
          this.error.set('Geolocation not supported');
          this.isLoading.set(false);
          reject('Geolocation not supported');
          return;
        }

        navigator.geolocation.getCurrentPosition(
          (position) => {
            const coords: LocationCoords = {
              latitude: position.coords.latitude,
              longitude: position.coords.longitude,
              accuracy: position.coords.accuracy,
              timestamp: position.timestamp
            };
            this.position.set(coords);
            this.isLoading.set(false);
            resolve(coords);
          },
          (err) => {
            const errorMsg = this.handleError(err);
            this.error.set(errorMsg);
            this.isLoading.set(false);
            reject(errorMsg);
          },
          {
            enableHighAccuracy: true,
            timeout: 10000,
            maximumAge: 0
          }
        );
      })
    );
  }

  private handleError(error: GeolocationPositionError): string {
    switch (error.code) {
      case error.PERMISSION_DENIED:
        return 'Location access denied. Please enable permissions.';
      case error.POSITION_UNAVAILABLE:
        return 'Location information unavailable.';
      case error.TIMEOUT:
        return 'Location request timed out.';
      default:
        return 'Unknown location error.';
    }
  }

  // ✅ Watch position changes
  watchPosition(): Observable<LocationCoords> {
    return new Observable(observer => {
      const watchId = navigator.geolocation.watchPosition(
        (position) => {
          const coords: LocationCoords = {
            latitude: position.coords.latitude,
            longitude: position.coords.longitude,
            accuracy: position.coords.accuracy,
            timestamp: position.timestamp
          };
          this.position.set(coords);
          observer.next(coords);
        },
        (err) => {
          observer.error(this.handleError(err));
        },
        { enableHighAccuracy: true }
      );

      return () => navigator.geolocation.clearWatch(watchId);
    });
  }
}
