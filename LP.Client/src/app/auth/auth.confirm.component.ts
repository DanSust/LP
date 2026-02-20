import { Component } from "@angular/core";
import { AuthService } from './../services/AuthService';
import { ActivatedRoute } from "@angular/router";

@Component({ template: '<p>Подтверждение...</p>' })
export class ConfirmComponent {
constructor(private authService: AuthService, private route: ActivatedRoute) {
    const token = route.snapshot.queryParamMap.get('token')!;
    const email = route.snapshot.queryParamMap.get('email')!;

  authService.confirmEmail(token, email);
    
  }
}
