import { Component, Input, inject, Signal, signal, effect, OnInit, OnDestroy, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { toSignal } from '@angular/core/rxjs-interop';
import { UserStatusService } from './../services/UserStatusService';
import { SignalRConnectionManager } from './../services/SignalRConnectionManager';
import { from } from 'rxjs';

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: "user-avatar.component.html",
  styleUrl: "user-avatar.component.scss"
})
export class UserAvatarComponent implements OnInit, OnDestroy {
  readonly userId = input<string>('');
  readonly name = input<string>('');

  // Сигнал для URL аватара с дефолтным значением
  //private avatarUrlSignal = signal<string>(`Photos/avatar/${this.userId()}`);
  private avatarUrlSignal = signal<string>('');

  // Публичный сигнал для шаблона
  public avatarUrl = this.avatarUrlSignal.asReadonly();

  private statService = inject(UserStatusService);
  private initialStatus: boolean = false;

  isOnline = computed(() => this.statService.onlineUsers().has(this.userId()));  

  constructor() {
    effect(() => {
      const id = this.userId();
      if (id) {
        this.avatarUrlSignal.set(`Photos/avatar/${id}`);
      }
    });
  }
    
  async ngOnInit(): Promise<void> {
    await this.statService.connect(this.userId());
  }

  onImageError(): void {    
    this.avatarUrlSignal.set('assets/default-avatar.svg');
  }

  ngOnDestroy(): void {    
  }
}
