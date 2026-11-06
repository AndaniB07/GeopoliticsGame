import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CountrySelectionPage } from './country-selection.page';

describe('CountrySelectionPage', () => {
  let component: CountrySelectionPage;
  let fixture: ComponentFixture<CountrySelectionPage>;

  beforeEach(() => {
    fixture = TestBed.createComponent(CountrySelectionPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
