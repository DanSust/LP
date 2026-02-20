import { Component, Inject, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from './../../app.config';

@Component({
  selector: 'user-profile-view',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './user-view.html',
  styleUrl: './user-view.scss'
})
export class UserView implements OnInit {
  @Input() userId: string | null = null; // Можно передавать ID профиля

  profile: any = null;
  isLoading: boolean = true;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string) { }

  ngOnInit() {
    // Если userId не передан, показываем текущего пользователя
    const id = this.userId || 'current';

    this.http.get<any>(`${this.base}/Users/view/${id}`, { withCredentials: true })
      .subscribe({
        next: (data) => {
          this.profile = data;
          this.isLoading = false;
        },
        error: (error) => {
          console.error('Failed to load profile:', error);
          this.isLoading = false;
        }
      });
  }
}
