import { Component, Inject, OnInit, HostListener, ElementRef, AfterViewInit, OnDestroy, ViewChild, ChangeDetectorRef, viewChild, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatButtonModule } from '@angular/material/button';
import { MatSliderModule } from '@angular/material/slider';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { FormControl, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL, DEFAULT_AVATAR_URL } from './../app.config';
import { RouterLink } from '@angular/router';
import { LocationService, LocationCoords } from './../services/LocationService';
import { PageStateService } from '../services/PageStateService';
import { NavigationService } from './../services/NavigationService';
import { formatDistance, getPeopleWord } from './../common/usefull.utils';
import { City, NearestCityResponse } from './../Interfaces/CityLocation';

import { ImageData } from './../Interfaces/ImageData';
import { PageState } from '../Interfaces/PageState';

import { catchError, map, Observable, of, startWith } from 'rxjs';
import { MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';

interface Interest {
  id: string;
  name: string;
}

interface SearchFilters {
  ageMin: number;
  ageMax: number;
  cityId: string | null;
  useGeolocation: boolean;
  latitude?: number;
  longitude?: number;
  radiusKm: number;
  page?: number;
  pageSize?: number;
}

@Component({
  selector: 'search-component',
  standalone: true,
  encapsulation: ViewEncapsulation.None,
  imports: [
    CommonModule,
    RouterLink,
    MatButtonModule,
    MatMenuModule,
    MatIconModule,
    MatSliderModule,
    MatSelectModule,
    MatFormFieldModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatAutocompleteModule,
    MatInputModule,
    FormsModule,
    ReactiveFormsModule
  ],
  templateUrl: './search.component.html',
  styleUrl: './search.component.scss'
})
export class SearchComponent implements OnInit, AfterViewInit, OnDestroy {
  galleryContainerRef = viewChild<ElementRef>('galleryContainer');

  protected readonly formatDistance = formatDistance;
  protected readonly getPeopleWord = getPeopleWord;

  allImages: ImageData[] = []; // Все загруженные изображения
  isLoading = true;
  isLoadingMore = false; // Флаг загрузки дополнительных данных
  baseUrl: string;
  defaultAvatarUrl: string;

  // 🔥 ФИЛЬТРЫ
  ageMin: number = 18;
  ageMax: number = 80;
  selectedInterests: string[] = [];
  availableInterests: Interest[] = [];

  // 🌍 ГОРОД И ГЕОЛОКАЦИЯ
  cityControl = new FormControl<City | string>('');
  availableCities: City[] = [];
  filteredCities: Observable<City[]> = of([]);
  selectedCityId: string = '';
  useGeolocation: boolean = false;
  radiusKm: number = 50;

  // Состояние геолокации
  isLocating = false;
  locationError: string | null = null;
  currentPosition: LocationCoords | null = null;
  private currentScrollPosition: number = 0;

  showFilters: boolean = true;

  // Пагинация
  currentPage: number = 1;
  pageSize: number = 20; // Количество элементов на странице
  hasMore: boolean = true; // Есть ли еще данные для загрузки

  // Дебаунс для скролла
  private scrollDebounceTimer: any;
  private scrollListener?: () => void;

  private isRestoringState = false;
  private shouldRestoreScroll = false;
  private savedScrollPosition: number = 0;
  

  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string,
    @Inject(DEFAULT_AVATAR_URL) private avatarUrl: string,
    private locationService: LocationService,
    private pageStateService: PageStateService,
    private navigationService: NavigationService,
    private cdr: ChangeDetectorRef
  ) {
    this.baseUrl = base;
    this.defaultAvatarUrl = avatarUrl;
  }

  ngOnInit(): void {
    const savedState = this.pageStateService.getState();
    const cameFromVote = this.navigationService.cameFrom('/vote') ||
      this.navigationService.cameFrom(/\/vote\/.*/);

    if (savedState && cameFromVote) {      
      this.restoreState(savedState);
    } else {
      // Обычная инициализация
      this.isLoading = true; // 🔥 Явно устанавливаем
      this.loadCities();
      this.setupCityAutocomplete();
    }
  }

  ngAfterViewInit(): void {
    if (this.shouldRestoreScroll) {
      this.restoreScrollPosition();
    }
    this.attachScrollListener();
  }

  ngOnDestroy(): void {
    this.saveStateBeforeLeave();
    // Убираем слушатель при уничтожении компонента
    if (this.scrollListener) {
      const container = this.galleryContainerRef()?.nativeElement;    
      if (container) {
        container.removeEventListener('scroll', this.scrollListener);
      }
    }
    clearTimeout(this.scrollDebounceTimer);
  }

  getImageUrl(photoId: string | null | undefined): string {
    console.log(`${this.baseUrl}/Photos/image/${photoId}`);
    if (!photoId || photoId === '00000000-0000-0000-0000-000000000000') {
      return 'assets/default-avatar.svg';//this.defaultAvatarUrl;
    }
    
    return `${this.baseUrl}/Photos/image/${photoId}`;
  }

  private restoreState(state: PageState): void {
    this.isRestoringState = true;

    // Восстанавливаем фильтры
    this.ageMin = state.filters.ageMin;
    this.ageMax = state.filters.ageMax;
    this.selectedCityId = state.filters.selectedCityId;
    this.useGeolocation = state.filters.useGeolocation;
    this.radiusKm = state.filters.radiusKm;
    this.showFilters = state.showFilters;

    // Восстанавливаем пагинацию
    this.currentPage = state.pagination.currentPage;
    this.hasMore = state.pagination.hasMore;
    this.savedScrollPosition = state.scrollPosition; 
    
    // Восстанавливаем изображения
    this.allImages = [...state.images];
    this.isLoading = false;
    
    // Восстанавливаем город в автокомплите
    if (state.filters.cityControlValue && this.availableCities.length > 0) {
      const city = this.availableCities.find(c => c.name === state.filters.cityControlValue);
      if (city) {
        this.cityControl.setValue(city);
      } else if (state.filters.cityControlValue) {
        this.cityControl.setValue(state.filters.cityControlValue);
      }
    }
    this.cdr.detectChanges();
    // Отмечаем, что нужно восстановить скролл
    this.shouldRestoreScroll = true;

    // Загружаем города если нужно
    if (this.availableCities.length === 0) {
      this.loadCities();
    }

    this.shouldRestoreScroll = true;
    this.isRestoringState = false;    
  }

  private restoreScrollPosition(): void {
    if (!this.shouldRestoreScroll) return;

    // 🔥 Пробуем получить контейнер разными способами
    let container = this.galleryContainerRef()?.nativeElement;

    if (!container) {
      container = document.querySelector('.gallery-search-container') as HTMLElement;
    }

    if (container) {
      container.scrollTop = this.savedScrollPosition;      

      // 🔥 Проверяем, действительно ли установилось
      setTimeout(() => {
        console.log('Actual scrollTop after restore:', container.scrollTop);
      }, 50);

      this.shouldRestoreScroll = false;
    } else {
      console.log('Cannot restore scroll: container=', !!container, 'position=', this.savedScrollPosition);
    }
  }

  // 🔥 Метод сохранения состояния
  private saveStateBeforeLeave(): void {    
    const container = this.galleryContainerRef()?.nativeElement;    
    const scrollPosition = container?.scrollTop || 0;
    

    const currentCityValue = this.cityControl.value;
    const cityName = typeof currentCityValue === 'string'
      ? currentCityValue
      : currentCityValue?.name || null;    

    this.pageStateService.saveState({
      images: this.allImages,
      filters: {
        ageMin: this.ageMin,
        ageMax: this.ageMax,
        selectedCityId: this.selectedCityId,
        useGeolocation: this.useGeolocation,
        radiusKm: this.radiusKm,
        cityControlValue: cityName
      },
      pagination: {
        currentPage: this.currentPage,
        hasMore: this.hasMore
      },
      scrollPosition: this.currentScrollPosition,
      showFilters: this.showFilters,
      timestamp: Date.now(),
      category: ''
    });
  }

  // Метод для подписки на скролл после загрузки данных
  private attachScrollListener(): void {
    setTimeout(() => {
      const galleryContainer = this.galleryContainerRef()?.nativeElement;
      if (galleryContainer) {        
        galleryContainer.addEventListener('scroll', this.onGalleryScroll.bind(this));
      } else {
        console.log('Gallery container not found');
      }
    }, 0);
  }

  setupCityAutocomplete(): void {
    this.filteredCities = this.cityControl.valueChanges.pipe(
      startWith(''),
      map(value => {
        const name = typeof value === 'string' ? value : value?.name;
        return name ? this._filterCities(name as string) : this.availableCities.slice();
      })
    );
  }

  private _filterCities(name: string): City[] {
    const filterValue = name.toLowerCase();
    return this.availableCities.filter(city =>
      city.name.toLowerCase().includes(filterValue)
    );
  }

  displayCityFn(city: City | string): string {
    return typeof city === 'string' ? city : city?.name || '';
  }

  onCitySelected(event: MatAutocompleteSelectedEvent): void {
    const city = event.option.value as City;
    this.selectedCityId = city.id;
    this.useGeolocation = false;
    this.currentPosition = null;
    this.resetPagination();
    this.onFilterChange();
  }

  loadCities(): void {
    this.http.get<City[]>(this.baseUrl + '/City/list', { withCredentials: true })
      .subscribe({
        next: (cities) => {
          this.availableCities = cities;
          if (this.isRestoringState) {
            const state = this.pageStateService.getState();
            if (state?.filters.cityControlValue) {
              const city = this.availableCities.find(c => c.name === state.filters.cityControlValue);
              if (city) {
                this.cityControl.setValue(city);
              }
            }
          } else {
            this.loadProfile();
          }
        },
        error: (err) => {
          console.error('Failed to load cities:', err);
          this.availableCities = [];
        }
      });
  }

  loadProfile(): void {
    this.http.get<any>(this.baseUrl + '/Users/info', { withCredentials: true })
      .subscribe({
        next: (user) => {          
          if (user.townId) {
            const city = this.availableCities.find(c => c.id === user.townId);

            if (city) {
              // Устанавливаем найденный город
              this.cityControl.setValue(city);
              this.selectedCityId = city.id;              
              //this.loadImages(true);
            }
          }
        },
        error: (err) => {
          console.error('Failed to load profile:', err);          
        }
      });
  }

  loadImages(isInitialLoad: boolean = false): void {    
    if (isInitialLoad) {
      this.isLoading = true;
      this.currentPage = 1;
      this.hasMore = true;
    } else {
      if (this.isLoadingMore || !this.hasMore) return;
      this.isLoadingMore = true;
    }

    const filters: SearchFilters = {
      ageMin: this.ageMin,
      ageMax: this.ageMax,
      cityId: this.selectedCityId || null,
      useGeolocation: this.useGeolocation,
      radiusKm: this.radiusKm,
      page: this.currentPage,
      pageSize: this.pageSize
    };

    if (this.useGeolocation && this.currentPosition) {
      filters.latitude = this.currentPosition.latitude;
      filters.longitude = this.currentPosition.longitude;
    }

    this.http.post<ImageData[]>(this.baseUrl + '/Votes/search', filters, { withCredentials: true })
      .subscribe({
        next: (data) => {
          // 🔥 Добавляем URL один раз при загрузке
          const processedData = data.map(img => ({
            ...img,
            imageUrl: this.getImageUrl(img.photoId)
          }));

          if (isInitialLoad) {
            this.allImages = processedData;
            this.isLoading = false;
            this.attachScrollListener();
          } else {
            if (data.length > 0) {
              this.allImages = [...this.allImages, ...processedData];
            }

            // Если пришло меньше данных, чем запрошено, значит это последняя страница
            if (data.length < this.pageSize) {
              this.hasMore = false;
            }

            this.isLoadingMore = false;
          }

          // Проверяем, если это первая загрузка и данных мало
          if (isInitialLoad && data.length === this.pageSize) {
            this.hasMore = true;
          }
        },
        error: (err) => {
          console.error('Failed to load images:', err);
          if (isInitialLoad) {
            this.isLoading = false;
          } else {
            this.isLoadingMore = false;
          }
        }
      });
  }

  loadMoreImages(): void {
    if (!this.isLoadingMore && this.hasMore) {
      this.currentPage++;
      this.loadImages();
    }
  }

  @HostListener('window:scroll')
  onWindowScroll(): void {
    if (this.showFilters) return;
    // Используем дебаунс для оптимизации
    clearTimeout(this.scrollDebounceTimer);
    this.scrollDebounceTimer = setTimeout(() => {
      this.checkScrollPosition();
    }, 100);
  }

  onGalleryScroll(): void {
    const container = this.galleryContainerRef()?.nativeElement;
    if (container) {
      this.currentScrollPosition = container.scrollTop;
    }

    if (this.showFilters || this.isLoadingMore || !this.hasMore) return;

    clearTimeout(this.scrollDebounceTimer);
    this.scrollDebounceTimer = setTimeout(() => {
      this.checkScrollPosition();
    }, 100);
  }

  checkScrollPosition(): void {
    // Получаем контейнер галереи
    const galleryContainer = document.querySelector('.gallery-search-container') as HTMLElement;
    if (!galleryContainer) return;

    const scrollTop = galleryContainer.scrollTop;
    const scrollHeight = galleryContainer.scrollHeight;
    const clientHeight = galleryContainer.clientHeight;

    // Порог срабатывания - 100px от конца
    const threshold = 100;

    if (scrollHeight - (scrollTop + clientHeight) < threshold) {
      this.loadMoreImages();
    }
  }

  onGeolocationClick(): void {
    this.isLocating = true;
    this.locationError = null;

    this.locationService.getCurrentPosition()
      .pipe(
        catchError(err => {
          this.locationError = err;
          this.isLocating = false;
          return of(null);
        })
      )
      .subscribe(coords => {
        if (coords) {
          this.currentPosition = coords;
          this.findNearestCity(coords.latitude, coords.longitude);
        } else {
          this.isLocating = false;
        }
      });
  }

  findNearestCity(latitude: number, longitude: number): void {
    this.http.get<NearestCityResponse>(`${this.baseUrl}/City/nearest`, {
      params: { latitude: latitude.toString(), longitude: longitude.toString() },
      withCredentials: true
    })
      .subscribe({
        next: (response) => {
          const city = response.city;
          this.cityControl.setValue(city);
          this.selectedCityId = city.id;
          this.useGeolocation = true;
          this.isLocating = false;
          this.resetPagination();
          //this.loadImages(true);
        },
        error: (err) => {
          console.error('Failed to find nearest city:', err);
          this.locationError = 'Не удалось найти ближайший город';
          this.isLocating = false;
        }
      });
  }

  requestGeolocation(): void {
    this.onGeolocationClick();
  }

  toggleGeolocation(): void {
    if (this.useGeolocation && !this.currentPosition) {
      this.onGeolocationClick();
    } else if (!this.useGeolocation) {
      this.selectedCityId = '';
      this.cityControl.setValue('');
    }
    this.resetPagination();
    this.onFilterChange();
  }

  onCityChange(): void {
    if (this.selectedCityId) {
      this.useGeolocation = false;
      this.currentPosition = null;
    }
    this.resetPagination();
    this.onFilterChange();
  }

  toggleFilters(): void {
    this.showFilters = !this.showFilters;
  }

  applyFilters(): void {    
    this.showFilters = false;
    this.resetPagination();
    this.pageStateService.clearState();
    this.loadImages(true);
  }

  onFilterChange(): void {
    // Можно добавить дебаунс для автоматической загрузки при изменении фильтров
  }

  clearFilters(): void {
    this.ageMin = 18;
    this.ageMax = 80;
    this.selectedInterests = [];
    this.selectedCityId = '';
    this.cityControl.setValue('');
    this.useGeolocation = false;
    this.radiusKm = 50;
    this.currentPosition = null;
    this.locationError = null;
    this.resetPagination();
    this.pageStateService.clearState();
  }

  resetPagination(): void {
    this.currentPage = 1;
    this.hasMore = true;
    this.allImages = [];
  }

  filteredImages(): ImageData[] {
    // Теперь просто возвращаем все загруженные изображения
    // Фильтрация уже происходит на сервере
    return this.allImages;
  }  

  get selectedCityName(): string {
    const city = this.availableCities.find(c => c.id === this.selectedCityId);
    return city?.name || 'Все города';
  }  
}
