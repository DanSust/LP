import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators, AbstractControl, ValidationErrors } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../services/AuthService';
import { ToastService } from '../common/toast.service';

function passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password')?.value;
  const confirmPassword = control.get('confirmPassword')?.value;
  return password === confirmPassword ? null : { mismatch: true };
}

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
  styleUrls: ['./auth.component.scss']
})
export class ResetPasswordComponent implements OnInit {
  form: FormGroup;
  isLoading = false;
  token = '';
  email = '';

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService,
    private toast: ToastService
  ) {
    this.form = this.fb.group({
      password: ['', [Validators.required, Validators.minLength(6)]],
      confirmPassword: ['', [Validators.required]]
    }, { validators: passwordMatchValidator });
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') || '';
    this.email = this.route.snapshot.queryParamMap.get('email') || '';
    if (!this.token || !this.email) {
      this.toast.error('Ссылка для восстановления пароля недействительна.');
    }
  }

  onSubmit(): void {
    if (!this.token || !this.email) {
      this.toast.error('Ссылка для восстановления пароля недействительна.');
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    const { password } = this.form.value;
    this.authService.resetPassword(this.email, this.token, password).subscribe({
      next: (response) => {
        this.isLoading = false;
        this.toast.success(response?.message || 'Пароль обновлён. Теперь вы можете войти.');
        this.router.navigate(['/auth']);
      },
      error: (error) => {
        this.isLoading = false;
        const message = error?.error?.message || 'Не удалось сбросить пароль. Запросите новую ссылку.';
        this.toast.error(message);
      }
    });
  }
}
