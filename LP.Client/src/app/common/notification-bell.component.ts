import { CommonModule } from "@angular/common";
import { Component, inject, Inject, OnInit } from "@angular/core";
import { API_BASE_URL } from "../app.config";
import { HttpClient } from "@angular/common/http";
import { EventsDTO } from "../models/DTO";
import { Router } from "@angular/router";

@Component({
  selector: 'notification-bell',
  templateUrl: './notification-bell.component.html',
  styleUrl: './notification-bell.component.scss',
  standalone: true,
  imports: [CommonModule]
})
export class NotificationBellComponent implements OnInit {
  router = inject(Router);
  count = 0;
  isMuted = true;


  constructor(private http: HttpClient, @Inject(API_BASE_URL) public base: string) { }

  ngOnInit(): void {
    this.http.get<EventsDTO[]>(this.base + '/Events/list', { withCredentials: true })
      .subscribe(u => {
        this.isMuted = !u.some(x => x.isNew === 1);
        console.log(u);
      });
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
