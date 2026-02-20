import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Output, OnInit } from '@angular/core';
import { Router } from '@angular/router';

interface Question {
  id: number;
  type: 'color' | 'sound';
  title: string;
  options: { id: string; label: string; value: string }[];
}

@Component({
  selector: 'app-demo-quest',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './demo-quest.component.html',
  styleUrls: ['./demo-quest.component.scss']
})
export class DemoQuestComponent implements OnInit {
  @Output() demoComplete = new EventEmitter<void>();

  currentStep = 0;
  selectedColor: string | null = null;
  selectedSound: string | null = null;

  // Имитация ответов "условного партнера" (AI)
  aiChoice = {
    color: this.getRandomColor(),
    sound: this.getRandomSound()
  };

  questions: Question[] = [
    {
      id: 1,
      type: 'color',
      title: '🎨 Выбери цвет, который описывает твое настроение прямо сейчас',
      options: [
        { id: 'red', label: '🔴 Энергия', value: '#e91e63' },
        { id: 'blue', label: '🔵 Спокойствие', value: '#03a9f4' },
        { id: 'green', label: '🟢 Надежда', value: '#4caf50' },
        { id: 'purple', label: '🟣 Творчество', value: '#9c27b0' },
        { id: 'yellow', label: '🟡 Радость', value: '#ffeb3b' },
        { id: 'orange', label: '🟠 Страсть', value: '#ff9800' }
      ]
    },
    {
      id: 2,
      type: 'sound',
      title: '🎵 Какой звук приятнее тысячи слов?',
      options: [
        { id: 'wave', label: '🌊 Волны', value: '/assets/sounds/wave.mp3' },
        { id: 'rain', label: '🌧 Дождь', value: '/assets/sounds/rain.mp3' },
        { id: 'fire', label: '🔥 Костер', value: '/assets/sounds/fire.mp3' }
      ]
    }
  ];

  syncPercentage = 0;
  showResult = false;

  constructor(private router: Router) { }

  ngOnInit(): void {
    // Предзагрузка звуков (опционально)
    //this.preloadAudio();
  }

  selectOption(optionId: string, type: 'color' | 'sound'): void {
    if (type === 'color') {
      this.selectedColor = optionId;
    } else {
      this.selectedSound = optionId;
    }

    // Автоматический переход через 1 секунду
    setTimeout(() => {
      this.nextStep();
    }, 1000);
  }

  nextStep(): void {
    if (this.currentStep < this.questions.length - 1) {
      this.currentStep++;
    } else {
      this.calculateResult();
    }
  }

  calculateResult(): void {
    let score = 0;

    // Сравниваем цвета (25%)
    if (this.selectedColor === this.aiChoice.color) {
      score += 25;
    } else if (this.isSimilarColor(this.selectedColor!, this.aiChoice.color)) {
      score += 15; // Похожие цвета
    }

    // Сравниваем звуки (25%)
    if (this.selectedSound === this.aiChoice.sound) {
      score += 25;
    }

    // Добавляем случайный фактор (AI не идеален)
    score += Math.floor(Math.random() * 50); // 0-50%

    this.syncPercentage = Math.min(score, 95); // Максимум 95%
    this.showResult = true;
  }

  private getRandomColor(): string {
    const colors = ['red', 'blue', 'green', 'purple', 'yellow', 'orange'];
    return colors[Math.floor(Math.random() * colors.length)];
  }

  private getRandomSound(): string {
    const sounds = ['wave', 'rain', 'fire'];
    return sounds[Math.floor(Math.random() * sounds.length)];
  }

  private isSimilarColor(color1: string, color2: string): boolean {
    // Похожие цвета: красный-оранжевый, синий-фиолетовый и т.д.
    const similarGroups = [
      ['red', 'orange'],
      ['blue', 'purple'],
      ['green', 'yellow']
    ];
    return similarGroups.some(group => group.includes(color1) && group.includes(color2));
  }

  private preloadAudio(): void {
    //this.questions[1].options.forEach(option => {
    //  const audio = new Audio();
    //  audio.src = option.value;
    //});
  }

  playSound(soundId: string): void {
    //const audio = new Audio(`/assets/sounds/${soundId}.mp3`);
    //audio.volume = 0.3;
    //audio.play().catch(() => { }); // Игнорируем ошибки автовоспроизведения
  }

  getAiChoiceForQuestion(type: 'color' | 'sound'): string {
    return type === 'color' ? this.aiChoice.color : this.aiChoice.sound;
  }

  closeDemo(): void {
    this.demoComplete.emit();
  }

  redirectToRegister(): void {
    this.router.navigate(['/auth']);
  }
}
