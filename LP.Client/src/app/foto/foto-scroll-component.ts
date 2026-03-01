import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { HttpClient } from '@angular/common/http';
//import { InterestDTO, TownDTO } from './../../DTO';
import { API_BASE_URL } from './../app.config'; 
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from './../services/AuthService';
import { NotificationBellComponent } from './../common/notification-bell.component';
import { first } from 'rxjs';

@Component({
  selector: 'foto-scroll-component',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatMenuModule, MatIconModule, NotificationBellComponent],  
  templateUrl: './foto-scroll-component.html',
  styleUrl: './foto-scroll-component.scss'  
})
export class FotoScrollComponent {
  images: any[] = [];
  isLoading = true;
  isMenuOpen = false;

  baseURL = "";

  //userId?: string = '';
  //isItMien: boolean = false;

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private router: Router,
    private route: ActivatedRoute,
    @Inject(API_BASE_URL) private base: string)
  {
    this.baseURL = base;
  }


  ngOnInit() {               // вызывается после конструктора
    //this.userId = this.route.snapshot.paramMap.get('id') ?? undefined;

    //this.authService.userId$
    //  .pipe(first()) // или take(1)
    //  .subscribe(currentUserId => {
    //    console.warn(currentUserId, this.userId);
    //    this.isItMien = currentUserId === this.userId;
    //  });

    this.http.get<string[]>(this.baseURL + '/Photos/scroll', { withCredentials: true })
      .subscribe(u => {
        this.images = u;
        this.isLoading = false;
      });
  }

  onImageClick(item: string) {
    console.log(item);
  }

  // Клик по аватару
  onAvatarClick(event: Event): void {
    event.stopPropagation(); // Важно! Предотвращает срабатывание routerLink родителя
    this.isMenuOpen = !this.isMenuOpen;
    console.log(event);
  }
};
