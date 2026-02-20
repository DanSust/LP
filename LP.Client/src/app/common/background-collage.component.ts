// background-collage.component.ts
import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { API_BASE_URL } from './../app.config';

@Component({
  selector: 'app-back-collage',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './background-collage.component.html',
  styleUrls: ['./background-collage.component.scss']
})
export class BackCollageComponent implements OnInit {
  photoUrls: string[] = [];

  constructor(@Inject(API_BASE_URL) public baseUrl: string) { }

  ngOnInit(): void {
    // Генерируем массив URL и перемешиваем
    const urls = Array.from({ length: 12 }, (_, i) =>
      `${this.baseUrl}/Photos/back/${i + 1}.jpg`
    );
    this.photoUrls = this.shuffleArray(urls);
  }

  private shuffleArray<T>(array: T[]): T[] {
    // Fisher-Yates shuffle algorithm
    const shuffled = [...array];
    for (let i = shuffled.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [shuffled[i], shuffled[j]] = [shuffled[j], shuffled[i]];
    }
    return shuffled;
  }
}
