import { Component, Input, inject, Signal, signal, effect, OnInit, OnDestroy, computed } from '@angular/core';
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
  @Input() userId!: string;
  @Input() name!: string;

  // Сигнал для URL аватара с дефолтным значением
  private avatarUrlSignal = signal<string>(`Photos/avatar/${this.userId}`);

  // Публичный сигнал для шаблона
  public avatarUrl = this.avatarUrlSignal.asReadonly();

  private statService = inject(UserStatusService);
  private initialStatus: boolean = false;

  isOnline = computed(() => this.statService.onlineUsers().has(this.userId));  

  constructor() {
  }
    
  async ngOnInit(): Promise<void> {
    await this.statService.connect(this.userId);

    this.avatarUrlSignal.set(`Photos/avatar/${this.userId}`);
  }

  onImageError(): void {
    this.avatarUrlSignal.set('assets/default-avatar.svg');
  }

  ngOnDestroy(): void {    
  }
}
