import { Injectable } from '@angular/core';
import { CanActivate, Router } from '@angular/router';
import { AuthService } from './../services/AuthService';
import { Observable } from 'rxjs';
import { map, take } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class AuthGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) { }

  canActivate(): Observable<boolean> {    
    return this.authService.userId$.pipe(
      take(1),
      map(userId => {
        if (userId && userId !== undefined) {
          if (userId === undefined)
            console.log('canActivate ' + userId);
          return true; // Пускаем
        } else {
          console.log('redirect to auth');
          this.router.navigate(['/auth']); // Перенаправляем на auth
          return false;
        }
      })
    );
  }
}
