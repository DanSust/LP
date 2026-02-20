// coming-soon.component.ts
import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy, Input } from '@angular/core';

@Component({
  selector: 'app-coming-soon',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './coming-soon.component.html',
  styleUrls: ['./coming-soon.component.scss']
})
export class ComingSoonComponent implements OnInit, OnDestroy {
  @Input() targetDate: Date = new Date(new Date().getFullYear(), 2, 1); // 1 марта по умолчанию
  @Input() title: string = 'Сайт в разработке';
  @Input() subtitle: string = 'Мы готовим что-то особенное для ваших сердец';

  daysLeft: number = 0;
  hoursLeft: number = 0;
  minutesLeft: number = 0;
  secondsLeft: number = 0;
  totalDays: number = 0;

  private timerInterval?: number;

  ngOnInit(): void {
    this.updateTimer();
    this.timerInterval = window.setInterval(() => {
      this.updateTimer();
    }, 1000);
  }

  ngOnDestroy(): void {
    if (this.timerInterval) {
      clearInterval(this.timerInterval);
    }
  }

  private updateTimer(): void {
    const now = new Date().getTime();
    const target = this.targetDate.getTime();
    const difference = target - now;

    if (difference > 0) {
      this.daysLeft = Math.floor(difference / (1000 * 60 * 60 * 24));
      this.hoursLeft = Math.floor((difference % (1000 * 60 * 60 * 24)) / (1000 * 60 * 60));
      this.minutesLeft = Math.floor((difference % (1000 * 60 * 60)) / (1000 * 60));
      this.secondsLeft = Math.floor((difference % (1000 * 60)) / 1000);
      this.totalDays = this.daysLeft + (this.hoursLeft / 24) + (this.minutesLeft / 1440);
    } else {
      this.daysLeft = 0;
      this.hoursLeft = 0;
      this.minutesLeft = 0;
      this.secondsLeft = 0;
    }
  }

  getProgressPercent(): number {
    // Прогресс от 30 дней до 0
    const maxDays = 30;
    const progress = Math.max(0, Math.min(100, ((maxDays - this.daysLeft) / maxDays) * 100));
    return progress;
  }
}
