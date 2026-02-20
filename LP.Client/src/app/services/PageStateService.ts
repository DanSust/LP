// services/pageStateService.ts
import { Injectable } from '@angular/core';
import { PageState } from '../Interfaces/PageState';



@Injectable({ providedIn: 'root' })
export class PageStateService {
  private state: PageState | null = null;
  private readonly MAX_AGE_MS = 5 * 60 * 1000; // 5 минут - время жизни состояния

  saveState(state: PageState): void {
    this.state = {
      ...state,
      timestamp: Date.now()
    };
    console.log('Search state saved:', this.state);
  }

  getState(): PageState | null {
    if (!this.state) return null;

    // Проверяем, не устарело ли состояние
    const age = Date.now() - this.state.timestamp;
    if (age > this.MAX_AGE_MS) {
      console.log('Search state expired, clearing');
      this.clearState();
      return null;
    }

    return this.state;
  }

  clearState(): void {
    this.state = null;
  }

  hasValidState(): boolean {
    return this.getState() !== null;
  }
}
