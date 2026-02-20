import { Component, Inject, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CdkDragDrop, CdkDrag, CdkDropList, moveItemInArray } from '@angular/cdk/drag-drop';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { API_BASE_URL } from './../../app.config'; //'../../main';
import { FileUploadComponent } from '../../common/file-upload';

@Component({
  selector: 'user-photo',
  standalone: true,
  templateUrl: './user-photo.html',
  styleUrl: './user-photo.css',
  imports: [
    CdkDrag, CdkDropList, MatIconModule,
    MatProgressSpinnerModule,
    FileUploadComponent],
})
export class UserPhoto {
  isSaving: boolean = false;
  items: { userId: string; id: string }[] = [];
  file: File | null = null; // Variable to store file
  constructor(
    private http: HttpClient,
    @Inject(API_BASE_URL) private base: string) {    
  }

  ngOnInit() {               // вызывается после конструктора
    this.loadData();
  }

  loadData() {
    this.http.get<{ userId: string, id: string }[]>(this.base + "/Photos/list", { withCredentials: true })
      .subscribe({
        next: (result) => {          
          //console.log('📸 Photos loaded:', result);
          this.items = result;
        },
        error: (err) => console.error('Error loading photos:', err)
      });
  }

  trackById(index: number, item: any): string {
    //console.log(item);
    return item.id; // Return unique identifier
  }

  drop(event: CdkDragDrop<string[]>) {
    this.isSaving = true;
    moveItemInArray(this.items, event.previousIndex, event.currentIndex);
    console.log(this.items[event.previousIndex].id);
    console.log(this.items[event.currentIndex].id);
    console.log(this.base);
    this.http.post(this.base + '/Photos/order/' + this.items[event.currentIndex].id, null, { withCredentials: true })
      .subscribe(() => { this.isSaving = false; });
  }

  // On file Select
  onChange(event: any) {
    const file: File = event.target.files[0];

    if (file) {
      //this.status = "initial";
      this.file = file;
    }
  }

  onDelete(id: string) {    
    this.http.post(/*this.base + "/Photos/delete/{id}"*/ `${this.base}/Photos/delete/${id}`, null, { withCredentials: true })
      .subscribe({
      next: () => {
        this.items = this.items.filter(item => item.id !== id);
      },
      error: (error: any) => {
        console.log(error);
        return (() => error);
      },
    });
  }

  onUpload() {
    if (this.file) {
      const formData = new FormData();

      formData.append('file', this.file, this.file.name);

      const upload$ = this.http.post<{userId: string; id: string} > (this.base + "/Photos/add", formData, { withCredentials: true });           

      upload$.subscribe(result => {        
        //console.log(result);
        this.items = [...this.items, result];        
      });
    }
  }

  onFileUploaded(newPhoto: { userId: string; id: string }): void {
    this.items = [...this.items, newPhoto]; // Add to parent's array
    console.log('✅ New photo added:', newPhoto);
  }
  
}
