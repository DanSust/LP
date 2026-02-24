// splash-screen.component.ts
import { Component, input } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-splash-screen',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './splash.component.html',
  styleUrl: './splash.component.scss'
})
export class SplashScreenComponent {
  visible = input.required<boolean>();
  progress = input<number>(0);
  showProgress = input<boolean>(true);
  text = input<string>('Загрузка...');
}
