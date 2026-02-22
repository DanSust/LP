import { ChangeDetectorRef, Component, Inject, ViewEncapsulation, OnInit } from '@angular/core';
import { AsyncPipe, CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, Validators, ReactiveFormsModule, FormControl, FormArray } from '@angular/forms';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatButtonModule } from '@angular/material/button';
import { MatFormField, MatInputModule, MatError } from '@angular/material/input';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatStepperModule } from '@angular/material/stepper';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatSliderModule } from '@angular/material/slider';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatAutocompleteModule, MatAutocompleteSelectedEvent } from '@angular/material/autocomplete'
import { MatChipInputEvent, MatChipListboxChange, MatChipsModule } from '@angular/material/chips';
import { format, subYears } from 'date-fns';
import { HttpClient } from '@angular/common/http';
import { InterestDTO, QuestionsDTO, TownDTO } from '../../models/DTO'; 
import { API_BASE_URL } from './../../app.config'; //'../../main';
import { UserPhoto } from '../user-photo/user-photo';
import { Observable } from 'rxjs/internal/Observable';
import { startWith } from 'rxjs/internal/operators/startWith';
import { map } from 'rxjs/internal/operators/map';
import { catchError, of } from 'rxjs';
import { ToastService } from '../../common/toast.service';
import { generateGUID } from '../../common/GUID';
import { MatIconModule } from '@angular/material/icon';
import { City, NearestCityResponse } from './../../Interfaces/CityLocation';
import { LocationService, LocationCoords } from './../../services/LocationService';

@Component({
  selector: 'user-profile',
  standalone: true,
  templateUrl: './user-profile.html',
  styleUrl: './user-profile.css',
  providers: [provideNativeDateAdapter()],
  encapsulation: ViewEncapsulation.None,
  imports: [
    //AsyncPipe,
    ReactiveFormsModule,
    FormsModule,    
    CommonModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
    MatStepperModule,
    MatIconModule,
    MatButtonModule,
    MatInputModule,
    MatSelectModule,
    MatError,
    MatFormField,
    MatDatepickerModule,
    MatSliderModule,
    MatChipsModule,
    MatAutocompleteModule,
    UserPhoto
  ]
})

export class UserProfile implements OnInit {
  form: ReturnType<typeof this.fb.group>;
  interests: InterestDTO[] = [];
  towns: TownDTO[] = [];
  filteredTowns: Observable<TownDTO[]> = of([]);
  isSaving: boolean = false;
  response: string = "";

  maxInterests = 10;
  selectedInterestsCount = 0;
  private previousSelectedIds: string[] = [];

  interestGroups = [
    { id: 1, name: 'Спорт' },
    { id: 2, name: 'культура' },
    { id: 3, name: 'Музыка' },
    { id: 4, name: 'Еда ' },
    { id: 5, name: 'природа' },
    { id: 6, name: 'Игры ' },
    { id: 7, name: 'обучение' },
    { id: 8, name: 'активность' }
  ];

  userQuestions: QuestionsDTO[] = [];
  maxQuestions = 3;

  // 🌍 ГЕОЛОКАЦИЯ (как в search.component)  
  isLocating = false;
  locationError: string | null = null;
  currentPosition: LocationCoords | null = null;
  useGeolocation: boolean = false;
  
  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private cdr: ChangeDetectorRef,
    private toast: ToastService,
    private locationService: LocationService,
    @Inject(API_BASE_URL) private base: string)
  {
    //console.log(base);
    this.form = this.fb.group({
      Email: ['', Validators.required],
      Caption: ['', Validators.required],
      Sex: '0',
      IsActive: true,
      SendEmail: true,
      WithPhoto: true,
      WithEmail: true,
      WithLikes: false,
      Birthday: subYears(new Date(), 18),
      Weight: 60,
      Height: 170,
      Description: '',
      TownName: ['', Validators.required],
      Town: null,      
      AgeFrom: 18,
      AgeTo: 100,
      Aim: 1,
      questions: this.fb.array([])
    });  
  }

  ngOnInit() {               // вызывается после конструктора    
    this.http.get<{ userId: string }>(this.base + '/Users/cookies')
      .subscribe(u => { console.log(u.userId) });

    this.http.get<InterestDTO[]>(this.base + '/Interests/list') // Ensure it's an array
      .subscribe(result => { this.interests = result; this.updateSelectedCount(); });

    this.http.get<TownDTO[]>(this.base + '/City/list', { withCredentials: true })
      .subscribe(u => {
        this.towns = u;
        this.setupCityAutocomplete();


    this.http.get<QuestionsDTO[]>(this.base + '/Questions/list', { withCredentials: true })
      .subscribe(questions => {
        while (this.questionsFormArray.length !== 0) {
          this.questionsFormArray.removeAt(0);
        }
        
        if (questions && questions.length > 0) {
          questions.forEach(q => this.addQuestion(q));
        } else {
          this.addQuestion(); // Add empty question
        }
      });

    this.http.get<any>(this.base + '/Users/info/', { withCredentials: true })
      .subscribe(u => {
        const town = this.towns.find(t => t.id === u.townId);

        if (town) {
          this.form.patchValue({
            TownName: town,
            Town: town.id
          });
        }

        console.log(u);

        this.form.patchValue({
          Email: u.email,
          Caption: u.caption,
          Sex: u.sex ? '1' : '0',
          IsActive: u.isPaused ? false : true,
          SendEmail: u.sendEmail ? false : true,
          WithPhotos: u.withPhotos ? false : true,
          WithEmail: u.withEmail ? false : true,
          WithLikes: u.withLikes ? false : true,
          Birthday: u.birthday,
          Weight: u.weight || 60,
          Height: u.height || 170,
          Description: u.description || '',
          //TownName: town || '',
          //Town: [this.towns],
          AgeFrom: u.ageFrom || 18,
          AgeTo: u.ageTo || 100,
          Aim: u.aim !== undefined ? String(u.aim) : '1'
        });

        for (const item of this.interests) {
          item.selected = false;
        }        
        for (const item of u.interests) {         
          const fitem = this.interests.find(iitem => iitem.id === item.id);
          if (fitem) {
            fitem.selected = true;            
          }
        } 
        //const inter = this.interests.find(t => t.id === u.intersts[0]);
      });    
    });

    this.filteredTowns = this.form.controls["TownName"].valueChanges.pipe(    
      startWith(''),
      map(value => this._filterTowns(value || ''))
    );
  }

  setupCityAutocomplete(): void {
    this.filteredTowns = this.form.controls["TownName"].valueChanges.pipe(
      startWith(''),
      map(value => {
        const name = typeof value === 'string' ? value : value?.name;
        return name ? this._filterTowns(name as string) : this.towns.slice();
      })
    );
  }

  private _filterTowns(name: string): TownDTO[] {
    const filterValue = name.toLowerCase();
    return this.towns.filter(city =>
      city.name.toLowerCase().includes(filterValue)
    );
  }

  displayCityFn(city: TownDTO | string | null): string {
    return typeof city === 'string' ? city : city?.name || '';
  }

  onCitySelected(event: MatAutocompleteSelectedEvent): void {
    const city = event.option.value as TownDTO;
    this.form.patchValue({
      TownName: city,
      Town: city.id
    });
    this.useGeolocation = false;
    this.currentPosition = null;
    this.locationError = null;
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
    this.http.get<NearestCityResponse>(`${this.base}/City/nearest`, {
      params: { latitude: latitude.toString(), longitude: longitude.toString() },
      withCredentials: true
    })
      .subscribe({
        next: (response) => {
          const city = response.city;
          this.form.patchValue({
            TownName: city,
            Town: city.id
          });
          this.useGeolocation = true;
          this.isLocating = false;
          this.locationError = null;
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


  // Метод для подсчета выбранных
  updateSelectedCount(): void {
    this.selectedInterestsCount = this.interests.filter(i => i.selected).length;
    this.previousSelectedIds = this.interests.filter(i => i.selected).map(i => i.id);
  }

  // Замените toggleSelected на onSelectionChange
  onSelectionChange(event: MatChipListboxChange): void {
    const newSelectedIds = event.value as string[];

    if (newSelectedIds.length > this.maxInterests) {
      // Восстанавливаем предыдущее состояние
      event.source.value = this.previousSelectedIds;
      alert(`Максимум ${this.maxInterests} интересов`);
      return;
    }

    // Обновляем состояние
    this.interests.forEach(item => item.selected = false);
    newSelectedIds.forEach(id => {
      const item = this.interests.find(i => i.id === id);
      if (item) item.selected = true;
    });

    // Сохраняем текущее состояние
    this.previousSelectedIds = [...newSelectedIds];
    this.selectedInterestsCount = newSelectedIds.length;
  }

  getInterestsByGroup(groupId: number): InterestDTO[] {
    return this.interests.filter(interest => interest.group === groupId);
  }

  getSelectedInterestIds(): string[] {
    return this.interests.filter(i => i.selected).map(i => i.id);
  }

  get questionsFormArray(): FormArray {
    return this.form.get('questions') as FormArray;
  }

  addQuestion(existingQuestion?: QuestionsDTO) {
    if (this.questionsFormArray.length >= this.maxQuestions) return;

    this.questionsFormArray.push(this.fb.group({
      id: [existingQuestion?.id || generateGUID()],
      question: [existingQuestion?.question || ''] // или question вместо name
    }));
  }

  getQuestionId(index: number): string {
    return this.questionsFormArray.at(index).value.id;
  }

  removeQuestion(index: number) { // ✅ Теперь принимает index
    if (this.questionsFormArray.length > 1) {
      this.questionsFormArray.removeAt(index);
    }
  }

  onQuestionChange(id: string, value: string) {
    const question = this.userQuestions.find(q => q.id === id);
    if (question) {
      question.question = value;
    }
  }

  ngAfterViewInit(): void {
    
    // ✅ Force Angular to re-check binding after view initializes
    setTimeout(() => {
      this.cdr.detectChanges();
    }, 0);
  }

  pause() {
    this.http.post(this.base + '/Users/pause/', null, { withCredentials: true })
      .subscribe({
      next: (response) => {        
      },
      error: (error) => {
        console.error('POST request failed:', error);
      }
    });
  }

  save() {
    if (this.form.valid) {
      const birthday = new Date(this.form.value.Birthday);
      
      // ✅ Конвертируем обратно для API
      birthday.setMinutes(birthday.getMinutes() - birthday.getTimezoneOffset());
      
      const questionsFromForm = this.questionsFormArray.value
        .filter((q: any) => q.question?.trim())
        .map((q: any, index: number) => ({
          id: q.id,
          question: q.question,
          order: index
        }));

      // Получаем ID города из формы
      const townId = this.form.value.Town || (this.form.value.TownName as TownDTO)?.id || '';

      this.isSaving = true;
      this.http.post<any>(this.base + '/Users/save', {
        ...this.form.value,
        Town: townId,
        Birthday: birthday.toISOString().split('T')[0],
        interests: this.interests,
        questions: questionsFromForm  
      }, { withCredentials: true })
        .subscribe({
          next: (response) => {
            //this.response = response.message;
            //console.log(response);
            this.toast.show(response.message);
            this.isSaving = false;
        },
        error: (error) => {
          console.error('POST request failed:', error);
          this.isSaving = false;
        }
      });
    }
  }
}
