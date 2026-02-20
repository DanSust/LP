// user-location.component.ts
import { ChangeDetectorRef, Component, Inject, OnInit } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { LocationService } from './../../services/LocationService';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from './../../app.config'; //'../../main';

@Component({
  selector: 'user-location',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule],
  templateUrl: './user-location.html'
})
export class UserLocationComponent {
  private locationService = Inject(LocationService);

  // ✅ Access signals directly in template
  position = this.locationService.position$;
  error = this.locationService.error$;
  isLoading = this.locationService.isLoading$;
  constructor(    
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
    @Inject(API_BASE_URL) private base: string) {
      console.log(base);    
  }

  getLocation(): void {
    this.locationService.getCurrentPosition().subscribe({
      next: (coords: string) => {
        console.log('✅ Location:', coords);
        // Send to backend
        this.http.post('/api/location', coords, {
          withCredentials: true
        }).subscribe();
      },
      error: (error: string) => console.error('❌ Failed:', error)
    });
  }

  startWatching(): void {
    this.locationService.watchPosition().subscribe({
      next: (coords: string) => console.log('📍 Position updated:', coords),
      error: (error: string) => console.error('❌ Watch error:', error)
    });
  }
}
