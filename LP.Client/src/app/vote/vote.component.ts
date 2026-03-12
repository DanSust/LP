import {
  Component,
  inject,
  signal,
  computed,
  OnInit,
  Inject,
  effect,
  ViewChild,
  ElementRef,
  AfterViewInit
} from '@angular/core';
import { CommonModule, Location } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { ProfileService } from './vote.service';
import { Profile } from './vote.model';
import { ActivatedRoute, Router } from '@angular/router';
import { API_BASE_URL } from './../app.config';
import { HttpClient } from '@angular/common/http';
import { ChatService } from '../services/ChatService';
import { ToastService } from '../common/toast.service';
import { NavigationService } from '../services/NavigationService';
import { getAgeWord } from './../common/usefull.utils';
import { Subscription } from 'rxjs';

interface ExitingProfile {
  id: string;
  name: string;
  photoUrl: string;
  direction: 'left' | 'right';
}

@Component({
  selector: 'vote-cards',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './vote.component.html',
  styleUrls: ['./vote.component.scss']
})
export class VoteComponent implements OnInit, AfterViewInit {
  @ViewChild('activePhotoContainer') photoContainer!: ElementRef<HTMLDivElement>;

  chatService = inject(ChatService);

  userId?: string = '';
  imageLoaded = signal(false);

  profileService = inject(ProfileService);
  profiles = this.profileService.profiles;

  // Улетающий профиль
  exitingProfile = signal<ExitingProfile | null>(null);

  // Состояние анимации
  isAnimating = signal(false);
  exitDelta = signal(0); // Смещение для улетающей карты

  currentPhotoIndex = signal(0);

  // Drag состояние для ручного свайпа
  dragDelta = signal(0);
  isDragging = signal(false);
  cameFromSearch = signal(false);

  isFullscreen = signal(false);
  isClick = signal(false);

  toggleFullscreen(): void {
    console.log('toggleFullscreen');
    this.isFullscreen.update(v => !v);
  }

  private readonly SWIPE_THRESHOLD = 350;
  private readonly LOAD_THRESHOLD = 3;
  private readonly BATCH_SIZE = 5;
  private readonly ANIMATION_DURATION = 500;

  getAgeWord = getAgeWord;

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string,
    private router: Router,
    private route: ActivatedRoute,
    private toast: ToastService,
    private navService: NavigationService,
    private location: Location
  ) {
    console.log('🏗️ VoteComponent создан');
    this.userId = this.route.snapshot.paramMap.get('id') ?? undefined;

    effect(() => {
      const count = this.profiles().length;
      // Подгружаем заранее, чтобы следующий профиль был готов
      if (count < this.LOAD_THRESHOLD + 1 && !this.userId) {
        console.log(`Осталось ${count} профилей, подгружаем...`);
        this.profileService.loadMoreProfiles(this.BATCH_SIZE);
      }
    });
  }

  private routeSub?: Subscription;
  ngOnInit(): void {   
    // 🔥 Подписываемся на изменения параметров маршрута
    this.routeSub = this.route.paramMap.subscribe(params => {
      const newUserId = params.get('id') ?? undefined;
      //console.log('Route params changed:', newUserId);      
      console.log('history:', this.navService.getPreviousUrl());

      this.resetState();

      this.userId = newUserId;
      this.currentPhotoIndex.set(0);
      this.isFullscreen.set(false);
      this.dragDelta.set(0);
      this.isDragging.set(false);

      if (this.userId) {
        this.profileService.loadProfile(this.userId);
        this.cameFromSearch.set(this.navService.cameFrom('/search') || this.navService.cameFrom('/match') || this.navService.cameFrom('/scroll'));
      } else {
        this.profileService.loadProfiles();
      }
    });
  }

  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();

    // Отменяем все подписки через AbortController
    //this.abortController.abort();

    // Очищаем все сигналы
    this.resetState();

    // Дополнительно удаляем все обработчики с элемента, если он еще существует
    if (this.photoContainer?.nativeElement) {
      const element = this.photoContainer.nativeElement;
      const clone = element.cloneNode(false);
      element.parentNode?.replaceChild(clone, element);
    }
  }

  ngAfterViewInit(): void {
    this.waitForElement();
    console.warn(this.userId);
    if (!this.userId) {
      this.setupSwipeGestures();
    }
    //this.setupPhotoClick();
  }

  private resetState(): void {
    // Сброс всех сигналов состояния
    this.currentPhotoIndex.set(0);
    this.isFullscreen.set(false);
    this.dragDelta.set(0);
    this.isDragging.set(false);
    this.exitingProfile.set(null);
    this.isAnimating.set(false);
    this.exitDelta.set(0);
    this.imageLoaded.set(false);
    this.hasExceededThreshold = false;

    // Очистка профилей — ключевой момент!
    this.profileService.clearProfiles();
  }

  private waitForElement(): void {
    const checkInterval = setInterval(() => {
      if (this.photoContainer?.nativeElement) {
        console.log('Element found');
        clearInterval(checkInterval);
        if (!this.userId) 
          this.setupSwipeGestures();
      }
    }, 100);

    // Очищаем интервал через 5 секунд, чтобы не было утечки
    setTimeout(() => clearInterval(checkInterval), 5000);
  }

  goBackToSearch(): void {
    //this.router.navigate(['/search']);
    if (window.history.length > 1) {
      this.location.back();
    } else {
      this.router.navigate(['/search']);
    }
  }

  private readonly DRAG_ACTIVATION_THRESHOLD = 40;  // пикселей — обычно 8–20

  
  private startY = 0;
  private hasExceededThreshold = false;

  // === SWIPE GESTURES ===
  private setupSwipeGestures(): void {
    console.log('setupSwipeGestures');
    const isFinePointer = window.matchMedia('(pointer: fine)').matches;

    const element = this.photoContainer?.nativeElement;
    if (!element) return;

    // 🔥 ПРОВЕРКА: игнорируем клики по нижней навигации
    const shouldIgnoreEvent = (e: Event): boolean => {
      const target = e.target as HTMLElement;
      // Игнорируем клики по bottom-nav и всему внутри него
      if (target.closest('.bottom-nav') || target.closest('app-bottom-nav')) {
        return true;
      }
      // Игнорируем клики по Material Menu
      if (target.closest('.mat-mdc-menu-panel') || target.closest('.cdk-overlay-container')) {
        return true;
      }
      return false;
    };

    const startDrag = (x: number, e: Event) => {
      if (isFinePointer) {        
        return;
      }

      // 🔥 Проверяем перед стартом свайпа
      if (shouldIgnoreEvent(e)) {
        console.log('Ignoring swipe: clicked on nav element');
        return;
      }

      const isFull = this.isFullscreen();
      
      if (isFull) return;
      if (this.isAnimating() || this.profiles().length < 2) return;
      this.isDragging.set(true);
      this.dragDelta.set(0);
      this.startX = x;

      
    };

    const moveDrag = (x: number) => {
      if (isFinePointer) {        
        return;
      }
      if (this.isFullscreen()) return;
      
      if (this.isAnimating()) return;

      const deltaX = x - this.startX;

      if (!this.hasExceededThreshold) {
        if (Math.abs(deltaX) < this.DRAG_ACTIVATION_THRESHOLD) {
          return;
        }
        this.hasExceededThreshold = true;        
      }

      this.dragDelta.set(deltaX);
    };

    const endDrag = () => {
      console.log('endDrag - ', this.isDragging());
      if (this.isFullscreen()) return;
      if (!this.isDragging()) return;

      const delta = this.dragDelta();
      this.isDragging.set(false);

      if (delta > this.SWIPE_THRESHOLD) {
        this.performSwipe('right');
      } else if (delta < -this.SWIPE_THRESHOLD) {
        this.performSwipe('left');
      } else {
        // Возврат с анимацией
        this.dragDelta.set(0);
      }
      this.hasExceededThreshold = false;
    };

    // Touch
    element.addEventListener('touchstart', (e: TouchEvent) => {
      startDrag(e.touches[0].clientX, e);
    }, { passive: true });

    element.addEventListener('touchmove', (e: TouchEvent) => {
      moveDrag(e.touches[0].clientX);
    }, { passive: true });

    element.addEventListener('touchend', endDrag);
    element.addEventListener('touchcancel', endDrag);

    // Mouse
    element.addEventListener('mousedown', (e: MouseEvent) => {
      startDrag(e.clientX, e);
    });

    document.addEventListener('mousemove', (e: MouseEvent) => {
      moveDrag(e.clientX);
    });

    document.addEventListener('mouseup', endDrag);
  }

  // vote.component.ts — замените setupClickDelegation на это:

  //private setupClickDelegation(): void {
  //  setTimeout(() => {
  //    const wrapper = this.photoContainer?.nativeElement;
  //    if (!wrapper) {
  //      console.error('photoContainer not found');
  //      return;
  //    }

  //    const img = wrapper.querySelector('.card-photo');
  //    if (!img) {
  //      console.error('img not found');
  //      return;
  //    }

  //    console.log('Setting up click on img:', img);

  //    // Удаляем старые обработчики если есть
  //    const newImg = img.cloneNode(true);
  //    img.parentNode?.replaceChild(newImg, img);

  //    // Вешаем клик на новый img
  //    newImg.addEventListener('click', (e: Event) => {
  //      console.log('IMG CLICKED!');
  //      e.stopPropagation();
  //      e.preventDefault();
  //      this.toggleFullscreen();
  //    });

  //    // Также вешаем на wrapper как fallback
  //    wrapper.addEventListener('click', (e: Event) => {
  //      const target = e.target as HTMLElement;
  //      // Если клик был по img, уже обработали выше
  //      if (target.tagName === 'IMG') return;

  //      // Игнорируем кнопки
  //      if (target.closest('button')) return;

  //      console.log('WRAPPER CLICKED');
  //      this.toggleFullscreen();
  //    });

  //  }, 100);
  //}

  //private setupPhotoClick(): void {
  //  const wrapper = this.photoContainer?.nativeElement;
  //  if (!wrapper) return;

  //  let startX = 0;
  //  let startY = 0;
  //  let startTime = 0;
  //  let isMoved = false;

  //  const handleStart = (clientX: number, clientY: number) => {
  //    startX = clientX;
  //    startY = clientY;
  //    startTime = Date.now();
  //    isMoved = false;
  //  };

  //  const handleMove = (clientX: number, clientY: number) => {
  //    const deltaX = Math.abs(clientX - startX);
  //    const deltaY = Math.abs(clientY - startY);
  //    if (deltaX > 10 || deltaY > 10) {
  //      isMoved = true;
  //    }
  //  };

  //  const handleEnd = (e: Event) => {
  //    const duration = Date.now() - startTime;

  //    // Это клик если: не двигали И быстро (менее 300ms)
  //    if (!isMoved && duration < 300) {
  //      e.stopPropagation();
  //      e.preventDefault();
  //      this.toggleFullscreen();
  //    }
  //  };

  //  // Mouse events
  //  wrapper.addEventListener('mousedown', (e: MouseEvent) => {
  //    handleStart(e.clientX, e.clientY);
  //  });

  //  wrapper.addEventListener('mousemove', (e: MouseEvent) => {
  //    handleMove(e.clientX, e.clientY);
  //  });

  //  wrapper.addEventListener('mouseup', (e: MouseEvent) => {
  //    handleEnd(e);
  //  });

  //  // Touch events — используем touch-action: none в CSS уже есть
  //  wrapper.addEventListener('touchstart', (e: TouchEvent) => {
  //    handleStart(e.touches[0].clientX, e.touches[0].clientY);
  //  }, { passive: true });

  //  wrapper.addEventListener('touchmove', (e: TouchEvent) => {
  //    handleMove(e.touches[0].clientX, e.touches[0].clientY);
  //  }, { passive: true });

  //  wrapper.addEventListener('touchend', (e: TouchEvent) => {
  //    handleEnd(e);
  //  });
  //}

  onPhotoClick(event: MouseEvent): void {
    if (this.isDragging() || this.dragDelta() !== 0) {
      //console.log('Ignored: dragging');
      return;
    }

    const target = event.target as HTMLElement;
    if (target.closest('.photo-arrow') ||
      target.closest('.photo-indicators') ||
      target.closest('.back-btn') ||      
      target.closest('button')) {
      //console.log('Ignored: clicked on button');
      return;
    }

    //console.log('Photo clicked, current fullscreen state:', this.isFullscreen());
    this.toggleFullscreen();
    //console.log('New fullscreen state:', this.isFullscreen());

    // Принудительно применяем изменение detection
    // иногда Angular не ловит изменения сигнала
    setTimeout(() => {     
      if (this.isFullscreen())
        this.profileService.addViewed(this.profiles()[0].id);
    }, 0);
  }

  private startX = 0;

  // === SWIPE LOGIC ===

  performSwipe(direction: 'left' | 'right', needSwipe: boolean = true): void {
    if (this.isDragging()) return;
    const profile = this.profiles()[0];
    if (!profile) return;

    console.log('needSwipe ', needSwipe);
    
    // 1. ЗАМОРАЖИВАЕМ текущий профиль для анимации вылета
    this.exitingProfile.set({
      id: profile.id,
      name: profile.name,
      photoUrl: profile.photoUrls?.[this.currentPhotoIndex()]?.path ?? '',
      direction
    });

    // 2. Удаляем из списка - следующий профиль становится profiles()[0]
    this.profileService.removeTopProfile(direction === 'right', needSwipe);

    // 3. Запускаем анимацию вылета
    this.isAnimating.set(true);
    this.exitDelta.set(0);

    // Форсируем reflow затем анимируем
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        this.exitDelta.set(direction === 'right' ? window.innerWidth : -window.innerWidth);
      });
    });

    // 4. Очищаем после анимации
    setTimeout(() => {
      this.isAnimating.set(false);
      this.exitingProfile.set(null);
      this.exitDelta.set(0);
      this.dragDelta.set(0);
      this.currentPhotoIndex.set(0);
    }, this.ANIMATION_DURATION);
  }

  // === STYLES ===

  getExitTransform(): string {
    const delta = this.exitDelta();
    const rotate = delta * 0.05;
    return `translateX(${delta}px) rotate(${rotate}deg)`;
  }

  getCurrentTransform(): string {
    if (!this.isDragging()) return '';
    const delta = this.dragDelta();
    const rotate = delta * 0.05;
    return `translateX(${delta}px) rotate(${rotate}deg)`;
  }

  getOverlayOpacity(): number {
    const delta = Math.abs(this.isDragging() ? this.dragDelta() : this.exitDelta());
    return Math.min(delta / this.SWIPE_THRESHOLD, 1) * 0.9;
  }

  // === ACTIONS ===

  onLikeClick(): void {
    console.log('onLikeClick()');
    this.performSwipe('right', !this.cameFromSearch());
  }

  onDislikeClick(): void {
    console.log('onDislikeClick()');
    this.performSwipe('left', !this.cameFromSearch());
  }

  onChatClick(id: string): void {
    console.log("[onChatClick] Запускаем для profile id:", id);

    this.http.post<any>(`${this.base}/chats/get-or-create/${id}`, null, { withCredentials: true })
      .subscribe({
        next: (response) => {
          console.log("[get-or-create] Полный ответ:", response);
          console.log("[get-or-create] userId →", response.userId);
          console.log("[get-or-create] chatId →", response.chatId);

          if (!response.userId) {
            console.warn("!!! Сервер НЕ вернул userId !!!");
          }

          this.chatService.activeUserId.set(response.userId);
          this.chatService.activeChatId.set(response.chatId);

          // ← можно добавить проверку
          setTimeout(() => {
            console.log("[после set] activeUserId теперь:", this.chatService.activeUserId());
          }, 0);

          this.router.navigateByUrl(`/chat/${response.chatId}`);
        },
        error: (err) => {
          if (err.error?.code === 'MUTUAL_LIKE_REQUIRED') {
            this.toast.warning('💕 Нужен взаимный лайк, чтобы начать переписку');
          } else {
            console.error('Ошибка:', err);
          }
        }
      });
  }

  onFavoriteClick(id: string): void {
    //console.log('⭐ Избранное:', id);
    this.profileService.addToFavorites(id);
  }

  nextPhoto(): void {
    if (this.isAnimating()) return;
    const photos = this.profiles()[0]?.photoUrls;
    if (!photos || photos.length <= 1) return;
    this.currentPhotoIndex.update(i => i >= photos.length - 1 ? 0 : i + 1);
    this.imageLoaded.set(false);
  }

  prevPhoto(): void {
    if (this.isAnimating()) return;
    const photos = this.profiles()[0]?.photoUrls;
    if (!photos || photos.length <= 1) return;
    this.currentPhotoIndex.update(i => i <= 0 ? photos.length - 1 : i - 1);
    this.imageLoaded.set(false);
  }

  onImageLoad(): void {
    this.imageLoaded.set(true);
  }
}
