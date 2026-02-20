// so-pulse.component.ts
import { CommonModule } from '@angular/common';
import { Component, OnInit, OnDestroy, ViewChild, ElementRef, Renderer2, AfterViewInit } from '@angular/core';
import { DemoQuestComponent } from '../demo/demo-quest.component';
import { Router } from '@angular/router';

@Component({
  selector: 'app-so-pulse',
  standalone: true,
  imports: [CommonModule, DemoQuestComponent],
  templateUrl: './so-pulse.component.html',
  styleUrls: ['./so-pulse.component.scss']
})
export class SoPulseComponent implements OnInit, OnDestroy, AfterViewInit {
  @ViewChild('heartLeft') heartLeft!: ElementRef<SVGSVGElement>;
  @ViewChild('heartRight') heartRight!: ElementRef<SVGSVGElement>;

  private syncTimeout?: number;
  private ecgAnimation?: Animation;
  showDemo = false;

  constructor(private renderer: Renderer2, private router: Router) { }

  ngOnInit(): void {
    this.renderer.removeClass(document.body, 'no-scroll');    
  }

  ngAfterViewInit(): void {
    // 🔥 Запускаем анимации ТОЛЬКО после полной отрисовки
    this.syncTimeout = window.setTimeout(() => {
      this.renderer.addClass(this.heartLeft.nativeElement, 'sync');
      this.renderer.addClass(this.heartRight.nativeElement, 'sync');
    }, 3000);

    this.initECGAnimation();
  }

  ngOnDestroy(): void {
    if (this.syncTimeout) {
      clearTimeout(this.syncTimeout);
    }
    if (this.ecgAnimation) {
      this.ecgAnimation.cancel();
    }
  }

  private initECGAnimation(): void {
    const ecgLine = document.querySelector('.ecg-line') as SVGPathElement;
    if (ecgLine && typeof ecgLine.animate === 'function') {
      this.ecgAnimation = ecgLine.animate([
        { strokeDashoffset: 400 },
        { strokeDashoffset: 0 }
      ], {
        duration: 1800,
        iterations: Infinity,
        easing: 'ease-in-out'
      });
    }
  }

  onPrimaryClick(): void {
    // Здесь можно добавить логику для основной кнопки
    console.log('Запуск сердцебиения');
    this.router.navigate(['/auth']);
    // Например, редирект на регистрацию или открытие модального окна
  }

  onSecondaryClick(): void {
    // Здесь можно добавить логику для вторичной кнопки
    console.log('Просмотр демо');
    this.showDemo = true;
  }

  onDemoComplete(): void {
    this.showDemo = false;
  }
}
