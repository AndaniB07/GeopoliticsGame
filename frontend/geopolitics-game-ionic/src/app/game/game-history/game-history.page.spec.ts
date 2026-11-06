import { ComponentFixture, TestBed } from '@angular/core/testing';
import { GameHistoryPage } from './game-history.page';

describe('GameHistoryPage', () => {
  let component: GameHistoryPage;
  let fixture: ComponentFixture<GameHistoryPage>;

  beforeEach(() => {
    fixture = TestBed.createComponent(GameHistoryPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
