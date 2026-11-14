import { CommonModule } from "@angular/common";
import { Component, inject, Inject } from "@angular/core";
import { API_BASE_URL } from "../app.config";
import { HttpClient } from "@angular/common/http";
import { EventsDTO } from "../models/DTO";
import { Router } from "@angular/router";
import { Observable, tap } from "rxjs";
import { BaseComponent } from "./base.component";

@Component({
  selector: 'notification-bell',
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.scss',
  standalone: true,
  imports: [CommonModule]
})
export class NotificationBellComponent extends BaseComponent {
  router = inject(Router);
  count = 0;
  isMuted = true;


  constructor(private http: HttpClient, @Inject(API_BASE_URL) public base: string) {
    super();
  }

  protected LoadData(): Observable<EventsDTO[]> {
    return this.http
      .get<EventsDTO[]>(this.base + '/Events/list', { withCredentials: true })
      .pipe(tap((events) => {
        this.isMuted = !events.some((event) => event.isNew === 1);
      }));
  }

  onBellClick(event: Event): void {
    event.stopPropagation();
    this.http.post<any>(this.base + '/Events/seen', null, { withCredentials: true })
      .subscribe(u => {
        this.isMuted = true;

        this.router.navigate(['/events']);
      });
    
  }
}
