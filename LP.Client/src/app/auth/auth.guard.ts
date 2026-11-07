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
        if (userId) return true;
        // Проверяем, что мы уже не на /auth
        if (!this.router.url.startsWith('/auth')) {
          this.router.navigate(['/auth'], { replaceUrl: true });
        }
        return false;
      })
    );
  }
}
