import { Component, Inject, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from './../../app.config';
import { BaseComponent } from './../../common/base.component';
import { SplashScreenComponent } from './../../splash/splash.component';

@Component({
  selector: 'user-profile-view',
  standalone: true,
  imports: [CommonModule, SplashScreenComponent],
  templateUrl: './user-view.html',
  styleUrl: './user-view.scss'
})
export class UserView extends BaseComponent {
  @Input() userId: string | null = null; // Можно передавать ID профиля

  profile: any = null;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string) {
    super();
  }

  protected LoadData(): Observable<any> {
    // Если userId не передан, показываем текущего пользователя
    const id = this.userId || 'current';

    return this.http
      .get<any>(`${this.base}/Users/view/${id}`, { withCredentials: true })
      .pipe(tap((data) => {
        this.profile = data;
      }));
  }
}
