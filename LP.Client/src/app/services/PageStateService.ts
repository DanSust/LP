// services/pageStateService.ts
import { Injectable } from '@angular/core';
import { PageState } from '../Interfaces/PageState';

@Injectable({ providedIn: 'root' })
export class PageStateService {
  private states = new Map<string, PageState>();
  private readonly MAX_AGE_MS = 5 * 60 * 1000; // 5 минут - время жизни состояния

  saveState(key: string, state: PageState): void;
  saveState(state: PageState): void;
  saveState(keyOrState: string | PageState, state?: PageState): void {
    let key = 'default';
    let nextState: PageState;

    if (typeof keyOrState === 'string') {
      key = keyOrState;
      nextState = state as PageState;
    } else {
      nextState = keyOrState;
      key = this.inferKey(nextState);
    }

    this.states.set(key, { ...nextState, timestamp: Date.now() });
  }

  getState(key: string = 'default'): PageState | null {
    const state = this.states.get(key);
    if (!state) return null;

    const age = Date.now() - state.timestamp;
    if (age > this.MAX_AGE_MS) {
      this.states.delete(key);
      return null;
    }

    return state;
  }

  clearState(key?: string): void {
    if (key) {
      this.states.delete(key);
      return;
    }
    this.states.clear();
  }

  /** Инвалидируем кэш /match после взаимного лайка, чтобы список «Взаимные» обновился. */
  clearMatchState(): void {
    this.states.delete('match');
  }

  hasValidState(key: string = 'default'): boolean {
    return this.getState(key) !== null;
  }

  private inferKey(state: PageState): string {
    const category = state?.category;
    if (category === 'mutual' || category === 'viewed' || category === 'likesMe' || category === 'iLike') {
      return 'match';
    }
    return 'search';
  }
}
