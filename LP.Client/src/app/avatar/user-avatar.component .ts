import { Component, inject, signal, effect, computed, input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { UserStatusService } from './../services/UserStatusService';

@Component({
  selector: 'app-user-avatar',
  standalone: true,
  imports: [CommonModule],
  templateUrl: 'user-avatar.component.html',
  styleUrl: 'user-avatar.component.scss'
})
export class UserAvatarComponent {
  readonly userId = input<string>('');
  readonly name = input<string>('');
  readonly showName = input<boolean>(true);

  private avatarUrlSignal = signal<string>('');
  public avatarUrl = this.avatarUrlSignal.asReadonly();

  private statService = inject(UserStatusService);

  isOnline = computed(() => this.statService.onlineUsers().has(this.userId()));

  constructor() {
    effect(() => {
      const id = this.userId();
      this.avatarUrlSignal.set(id ? `Photos/avatar/${id}` : 'assets/default-avatar.svg');
    });
  }

  onImageError(): void {
    this.avatarUrlSignal.set('assets/default-avatar.svg');
  }
}
