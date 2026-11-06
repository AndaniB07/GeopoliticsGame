import { Injectable } from "@angular/core";
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { AuthService } from "./auth";
import { ControlContainer } from "@angular/forms";
import { environment } from '../../environments/environment';


export interface Country {
    id: string;
    name: string;
    continent: string;
    populationMillions: number;
}

export interface GameStatistics {
    gameId: string;
    playerCountryName: string;
    round: number;
    status: string;
    outcome: string;
    countriesControlled: number;
    totalSoldiers:number;
    countriesConquered: number;
    countriesLost: number;
    battlesWon: number;
    battlesLost: number;
    totalBattleRounds: number;
}


@Injectable({
    providedIn: 'root'
})

export class GameService {
    private readonly apiUrl = `${environment.apiUrl}/Games`;

    constructor(
        private http: HttpClient,
        private authService: AuthService
    ){}

    private getAuthHeaders(): HttpHeaders {
        const token = this.authService.getToken();

        return new HttpHeaders({
            Authorization: `Bearer ${token}`
        });
    }

    getCountries() {
        return this.http.get<Country[]>(
    `${environment.apiUrl}/Countries`
);
    }

    createGame(playerCountryId: string){
        return this.http.post<any>(
            this.apiUrl,
            {
                playerCountryId: playerCountryId
            },
            {
                headers: this.getAuthHeaders()
            }
        );
    }

    getGame(gameId: string){
        return this.http.get<any>(
            `${this.apiUrl}/${gameId}`,
            {
                headers: this.getAuthHeaders()
            }
        );
    }

    getMyGames() {
        return this.http.get<any[]>(
            `${this.apiUrl}/my-games`,
            {
                headers: this.getAuthHeaders()
            }
        );
    }

    getGameStatistics(gameId: string) {
        return this.http.get<GameStatistics>(
            `${this.apiUrl}/${gameId}/statistics`
        );
    }

    
allocateReinforcements(
  gameId: string,
  allocations: { countryId: string; soldiers: number }[]
) {
  return this.http.post<any>(
    `${this.apiUrl}/${gameId}/allocate-reinforcements`,
    {
      allocations: allocations
    },
    {
      headers: this.getAuthHeaders()
    }
  );
}


    attack(
        gameId:  string,
        attackerCountryId: string,
        defenderCountryId: string,
        continueAttack: boolean = true

    ){
        return this.http.post<any>(
            `${environment.apiUrl}/Battle/${gameId}/attack`,
            {
                attackerCountryId: attackerCountryId,
                defenderCountryId: defenderCountryId,
                continueAttack: continueAttack
            },
            {
                headers: this.getAuthHeaders()
            }
        );
    }

    advanceTurn (gameId: string) {
        return this.http.post<any>(
            `${this.apiUrl}/${gameId}/advanced-turn`,
            {},
            {
                headers: this.getAuthHeaders()
            }
        );
    }

    
    transferConquestSoldiers(
        gameId: string,
        soldiersToTransfer: number
        ) {
            return this.http.post<any>(
            `${this.apiUrl}/${gameId}/transfer-conquest-soldiers`,
            {
                soldiersToTransfer: soldiersToTransfer
            },
            {
                headers: this.getAuthHeaders()
            }
        );
    }

    transferSoldiers(
  gameId: string,
  sourceCountryId: string,
  destinationCountryId: string,
  soldiers: number
) {
  return this.http.post<any>(
    `${this.apiUrl}/${gameId}/transfer-soldiers`,
    {
      sourceCountryId: sourceCountryId,
      destinationCountryId: destinationCountryId,
      soldiers: soldiers
    },
    { headers: this.getAuthHeaders() }
  );
}




}