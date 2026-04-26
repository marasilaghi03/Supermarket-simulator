# Supermarket simulator
Proiectul constă într-un simulator 2D de tip grid-based al unui supermarket, în care mai mulți 
clienți (NPC-uri) se deplasează prin magazin și încearcă să își finalizeze lista de cumpărături. 
Fiecare client va căuta produsele dorite, va naviga printre rafturi, va ridica produsele și va merge 
la casă, într-un mediu în care există și alți clienți care le influențează deciziile (aglomerare, cozi, 
disponibilitatea produselor). 

Focusul proiectului este pe comportamentul autonom al acestor agenți și pe modul în care aceștia 
iau decizii într-un mediu dinamic. Pentru modelarea comportamentului general voi folosi Finite 
State Machine, iar pentru deplasare un algoritm de pathfinding pe grilă. Partea principală a 
proiectului va fi compararea a două metode diferite de luare a deciziilor: Decision Tree (bazat pe 
reguli) și Utility AI (bazat pe scoruri), pentru a observa diferențele de comportament și eficiență. 

Scopul proiectului este de a evidenția cum influențează alegerea algoritmului de AI 
comportamentul agenților și rezultatele obținute în simulare, folosind metrici precum timpul 
petrecut în magazin, timpul de așteptare la coadă sau numărul de clienți care părăsesc magazinul 
fără să cumpere.
