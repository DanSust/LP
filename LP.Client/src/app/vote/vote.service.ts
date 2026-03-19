import { Inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, BehaviorSubject, of, delay, tap } from 'rxjs';
import { API_BASE_URL } from './../app.config';
import { getZodiacSign  } from './../common/usefull.utils';
import { Profile } from './vote.model';
import { ToastService } from '../common/toast.service';

@Injectable({ providedIn: 'root' })
export class ProfileService {
 
  constructor(
    private http: HttpClient,
    private toast: ToastService,
    @Inject(API_BASE_URL) private baseUrl: string
  ) { }

  profiles = signal<Profile[]>([]);
  isLoading = signal(false);
  private buffer: Profile[] = [];

  private calculateAge(birthday: string): number {
    if (!birthday) return 0;
    const birthDate = new Date(birthday);
    const today = new Date();
    let age = today.getFullYear() - birthDate.getFullYear();
    const monthDiff = today.getMonth() - birthDate.getMonth();
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < birthDate.getDate())) {
      age--;
    }
    return age;
  }

  public clearProfiles(): void {
    this.profiles.set([]);
    this.buffer = [];
    this.lastLoadEmpty = false;
  }

  private lastLoadEmpty = false;
  // Динамическая подгрузка профилей
  loadMoreProfiles(count: number = 5) {
    if (this.isLoading()) return; // Предотвращаем двойную загрузку

    if (this.lastLoadEmpty) {
      console.log('Профилей больше нет на сервере');
      return;
    }

    this.isLoading.set(true);
    console.log(`Загружаем ${count} профилей...`);
    

    this.http.get<any>(`${this.baseUrl}/Votes/list?count=${count}`, { withCredentials: true })
      .subscribe({
        next: (response) => {
          if (!response || response.length === 0) {
            this.lastLoadEmpty = true; // 🚩 Запоминаем, что профилей больше нет
            this.isLoading.set(false);
            return;
          }

          //console.warn(response);

          const newProfiles: Profile[] = response.map((p: any) => ({
            id: p.id,
            name: p.name,
            city: p.city,
            age: this.calculateAge(p.birthday),
            photoUrls: p.photos?.map((photo: any) => ({
              id: photo.id,
              path: `${this.baseUrl}/Photos/image/${photo.id}`
            })) || [],
            interests: p.interests?.map((interest: any) => ({
              id: interest.id,
              name: interest.name,
              path: interest.path
            })) || []
          }));

          // Добавляем к текущим профилям
          //this.profiles.update(profiles => [...profiles, ...newProfiles]);
          // Оставляем только те профили, которых еще нет в текущем списке по ID
          // Обновляем список, оставляя только уникальные ID
          this.profiles.update(currentList => {
            const uniqueNew = newProfiles.filter(np =>
              !currentList.some(existing => existing.id === np.id)
            );
            return [...currentList, ...uniqueNew];
          });

          this.isLoading.set(false);
          console.log(`Загружено ${newProfiles.length} профилей. Всего: ${this.profiles().length}`);
          console.log(this.profiles()[0]);
        },
        error: (error) => {
          console.error('Failed to load profiles:', error);
          this.isLoading.set(false);
        }
      });
  }

  loadProfile(id: string) {    
    this.http.get<any>(`${this.baseUrl}/Users/view?id=${id}`, { withCredentials: true })
      .subscribe({
        next: (response) => {
          console.log(response);
          const profile: Profile = {
            id: response.id,
            name: response.caption,
            city: response.cityName || '',
            age: this.calculateAge(response.birthday),
            //sex: response.sex ? true : false,
            
            height: response.height,
            weight: response.weight,
            interests: response.interests,
            description: response.description,
            zodiac: getZodiacSign(response.birthday)
            //bio: '',
            //distance: '',
            // Остальные поля могут быть пустыми для публичного профиля
          };
          this.profiles.set([profile]);
          this.http.get<any[]>(`${this.baseUrl}/Photos/user?id=${id}`, { withCredentials: true })
            .subscribe(img => {              
              profile.photoUrls = img.map(photo => ({
                id: photo.id,
                path: `${this.baseUrl}/Photos/image/${photo.id}`
              }));
              
              this.profiles.set([profile]);              
            });
        },
        error: (error) => {
          console.error('Failed to load profile:', error);
          this.toast.error('Пользователь не найден');
          
        }
      });
  }

  loadProfiles(count: number = 1) {
    this.loadMoreProfiles(count);
  }

  removeTopProfile(like: boolean = true, needRemove: boolean = true) {
    const currentProfile = this.profiles()[0];
    const lk = like ? 'like' : 'dislike';

    if (needRemove) {
      this.profiles.update(profiles => profiles.slice(1));
      console.log('removeTopProfile -', this.profiles());
    }
    
    this.http.post<any>(this.baseUrl + '/Votes/' + lk + '/' + currentProfile.id, null, { withCredentials: true })
      .subscribe({
        next: () => {
          console.log('Лайк отправлен', currentProfile.id)
        },
        error: (error: any) => {
          console.log(error);
          return (() => error);
        },
      });    
  }

  addProfiles(newProfiles: Profile[]) {
    //this.profiles.update(profiles => [...profiles, ...newProfiles]);
    this.profiles.update(current => {
      // Оставляем только те профили, которых еще нет в текущем списке по ID
      const uniqueNew = newProfiles.filter(np => !current.some(c => c.id === np.id));
      return [...current, ...uniqueNew];
    });
  }

  addToFavorites(id: string) {
    //return this.http.post(`${this.baseUrl}/Users/update-name`, { id }, { withCredentials: true });
    this.http.post<any>(this.baseUrl + '/Users/update-name/' + id, null, { withCredentials: true })
      .subscribe({
        next: () => {          
          console.log('Update отправлен')
        },
        error: (error: any) => {
          console.log(error);
          return (() => error);
        },
      });
  }

  addViewed(id?: string) {    
    this.http.post<any>(this.baseUrl + '/Votes/viewed/' + id, null, { withCredentials: true })
      .subscribe({
        next: () => {
          console.log('Viewed отправлен')
        },
        error: (error: any) => {
          console.log(error);
          return (() => error);
        },
      });
  }
}
