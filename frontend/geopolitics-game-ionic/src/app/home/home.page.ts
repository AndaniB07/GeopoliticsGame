import { Component } from '@angular/core';
import {  IonContent, IonIcon, IonButton } from '@ionic/angular';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
  imports: [ IonContent, IonButton, IonIcon, RouterLink],
})
export class HomePage {
  constructor() {}
}
