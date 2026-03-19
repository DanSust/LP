import { Component, Input, OnInit, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from './../../app.config';

@Component({
  selector: 'app-rating',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="rating-badge" [class.small]="size === 'small'" [class.large]="size === 'large'">
      <div class="stars-container">
        <div class="stars">
          @for (star of stars; track $index) {
            <span class="star" [class.filled]="$index < filledStars" 
                  [class.half]="showHalfStar && $index === halfStarIndex">
              ★
            </span>
          }
        </div>
        <span class="rating-value">{{ displayRating.toFixed(1) }}</span>
      </div>
    </div>
  `,
  styles: [`
    .rating-badge {
      position: absolute;
      top: 15px;
      left: 15px;
      z-index: 30;
      background: rgba(0, 0, 0, 0.0);
      padding: 5px 12px;      
    }

    .stars-container {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .stars {
      display: flex;
      gap: 3px;
      color: rgba(255, 255, 255, 0.3);
    }

    .star {
      font-size: 18px;
      line-height: 1;
      transition: all 0.2s;
      
      &.filled {
        color: #FFD700;
        text-shadow: 0 0 10px rgba(255, 215, 0, 0.5);
      }

      &.half {
        position: relative;
        color: rgba(255, 255, 255, 0.3);
        
        &::after {
          content: '★';
          position: absolute;
          left: 0;
          top: 0;
          width: 50%;
          overflow: hidden;
          color: #FFD700;
          text-shadow: 0 0 10px rgba(255, 215, 0, 0.5);
        }
      }
    }

    .rating-value {
      font-size: 14px;
      color: white;
      font-weight: 600;
      text-shadow: 0 2px 4px rgba(0, 0, 0, 0.3);
    }

    /* Размеры */
    .small {
      padding: 3px 8px;
      
      .star {
        font-size: 14px;
      }
      
      .rating-value {
        font-size: 12px;
      }
    }

    .large {
      padding: 8px 16px;
      
      .star {
        font-size: 22px;
      }
      
      .rating-value {
        font-size: 16px;
      }
    }

    /* Адаптация для мобильных */
    @media (max-width: 768px) {
      .rating-badge {
        top: 10px;
        left: 10px;
        padding: 4px 10px;
      }

      .star {
        font-size: 16px;
      }

      .rating-value {
        font-size: 12px;
      }
    }

    /* В полноэкранном режиме */
    :host-context(.fullscreen) .rating-badge {
      background: rgba(0, 0, 0, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.3);
      z-index: 1000002; /* Выше стрелок */
    }
  `]
})
export class RatingComponent implements OnInit, OnChanges {
  @Input() profileId!: string;
  @Input() size: 'small' | 'medium' | 'large' = 'medium';
  @Input() showValue: boolean = true;
  @Input() fixedRating?: number; // Опционально: можно передать фиксированное значение

  protected displayRating: number = 0;
  protected stars = Array(5).fill(0);
  protected filledStars = 0;
  protected showHalfStar = false;
  protected halfStarIndex = 0;
  private http = inject(HttpClient);
  private baseUrl = inject(API_BASE_URL);

  private ratingCache = new Map<string, number>();

  ngOnInit() {
    this.loadRating();
  }

  ngOnChanges(changes: SimpleChanges) {
    if (changes['profileId'] && !changes['profileId'].firstChange) {
      this.loadRating();
    }
  }

  private calculateRating(): void {
    // Если передан фиксированный рейтинг - используем его
    if (this.fixedRating !== undefined) {
      this.displayRating = this.fixedRating;
    } else {
      this.displayRating = this.getRandomRating(this.profileId);
    }
    this.updateStars();
  }

  private loadRating(): void {
    if (!this.profileId) return;
    
    this.http.get<any>(`${this.baseUrl}/Users/rating/${this.profileId}`)
      .subscribe({
        next: (response) => {
          this.displayRating = response.rating;
          this.updateStars();          
        },
        error: (err) => {
          console.error('Error loading rating:', err);
          // Fallback на случайный рейтинг при ошибке
          this.displayRating = this.getRandomRating(this.profileId);
          this.updateStars();          
        }
      });
  }

  private getRandomRating(profileId: string): number {
    if (!profileId) return 0;

    if (!this.ratingCache.has(profileId)) {
      // Генерируем число от 0 до 10 с одним знаком после запятой
      // Используем profileId как seed для стабильности
      const seed = profileId.split('').reduce((acc, char) => acc + char.charCodeAt(0), 0);
      const random = (Math.sin(seed) * 10000) - Math.floor(Math.sin(seed) * 10000);
      const rating = Math.round(random * 100) / 10; // 0-10 с одним знаком после запятой
      this.ratingCache.set(profileId, Math.min(10, Math.max(0, rating)));
    }
    return this.ratingCache.get(profileId) || 0;
  }

  private updateStars(): void {
    // Конвертируем рейтинг из 10-балльной шкалы в 5-звездочную
    const starRating = this.displayRating / 2; // 0-10 -> 0-5
    this.filledStars = Math.floor(starRating);
    this.showHalfStar = starRating - this.filledStars >= 0.5;
    this.halfStarIndex = this.filledStars;
  }
}
