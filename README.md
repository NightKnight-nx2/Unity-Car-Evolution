# UnityCarEvolution

A top-down 2D car-driving simulation (top-down camera, real 3D `WheelCollider` physics underneath) where cars evolve to drive using raycast distance sensors, a small feedforward neural network, and a classic genetic algorithm.

**Language / Dil:** [English](#english) · [Türkçe](#türkçe)

---

## English

### Overview

Each car has 3-7 forward-facing raycast sensors that measure distance to the track walls. Those readings feed a small feedforward neural network (inputs: sensor distances, outputs: steering angle + throttle/brake) that drives the car. The network's weights are trained with a classic genetic algorithm: a population of cars drives in parallel every generation, the best performers are selected, and the next generation is produced via crossover + mutation.

The project also includes an open, point-to-point **maze mode**: all cars spawn from the exact same point and try to reach a single finish point, with an A* pathfinder shaping the fitness signal toward the goal (A* only scores progress — it never drives the car; the neural network + GA remain fully in control of driving).

### Requirements

- Unity Editor **6000.3.1f1** ("Unity 6.3 LTS") or a compatible Unity 6 version.
- The project uses `WheelCollider`, so `com.unity.modules.vehicles` must stay listed in `Packages/manifest.json` (it's already there).

### Setup

1. Open this folder in Unity Hub ("Add project from disk").
2. Run these menu items in order from the top menu bar:
   - **Car Evolution > 1. Create Data Assets (Profiles + Tracks)** — creates the Formula/Rally/Snowmobile vehicle profiles under `Assets/Data/Profiles`, and the WideOval / TightChicane / **Maze1** track assets under `Assets/Data/Tracks`.
   - **Car Evolution > 2. Build Simulation Scene** — builds `Assets/Scenes/MainScene.unity` from scratch: track (WideOval), car prefab (with `WheelCollider`s), `PopulationManager`, `SimulationConfig`, top-down camera, HUD canvas.
   - **Car Evolution > 3. Build Maze Scene** — builds `Assets/Scenes/MazeScene.unity` the same way but using the `Maze1` track (see [Maze mode](#maze-mode-single-start--finish-a-guided-reward) below).
3. Press **Play**. The first generation starts immediately; the HUD (top-left) shows the current generation number, best fitness, how many cars are still alive, and live stats for the current best car.

All three menu items are idempotent for assets: they won't recreate a profile/track asset that already exists. Building a scene, however, always rebuilds that `.unity` file from scratch (so any manual edits you made directly in that scene get overwritten — re-run the menu item any time you want a fresh scene).

### Architecture

```
Assets/Scripts/
  Sensors/CarSensors.cs          - fires N raycasts, returns normalized distances
  NeuralNet/NeuralNetwork.cs     - input -> hidden(tanh) -> output(tanh), flat float[] weights
  GA/Genome.cs, GeneticAlgorithm.cs
                                  - elitism + tournament selection + crossover + mutation
  Car/VehicleProfile.cs          - Formula/Rally/Snowmobile: WheelCollider parameters
  Car/CarController.cs           - steer + throttle/brake -> WheelCollider physics
  Car/CarAgent.cs                - ties sensors+brain+controller together, computes fitness
  Track/TrackDefinition.cs       - a track = 2D centerline + width (data-only asset)
  Track/TrackBuilder.cs          - turns a centerline into walls + road mesh + checkpoints
  Track/CheckpointTrigger.cs
  Pathfinding/MazeGrid.cs        - walkable/blocked cell grid built from a centerline
  Pathfinding/AStarPathfinder.cs - A* (FindPath) + a Dijkstra-equivalent bulk distance field
  Pathfinding/GoalDistanceField.cs - "true (maze-aware) distance to the finish" - O(1) lookup
  Simulation/SimulationConfig.cs - every Inspector-configurable parameter
  Simulation/PopulationManager.cs - the generation loop: spawn -> simulate -> evolve
  UI/SimulationHUD.cs            - generation/fitness text + the best car's sensor rays
  Benchmark/BenchmarkReportUI.cs - compares two PopulationManagers side by side
Assets/Editor/CarEvolutionSetup.cs
                                  - one-click data asset + scene bootstrap (MainScene + MazeScene)
```

On a **closed-loop track** (WideOval, TightChicane), fitness = distance covered + checkpoints passed * bonus + survival time. On the **open maze track**, fitness is instead driven by real A*-measured progress toward the finish (see [Maze mode](#maze-mode-single-start--finish-a-guided-reward)). Either way, a car "dies" (`IsAlive = false`) when it hits a wall, gets physically launched off the track, or stays nearly stationary for `stuckTimeout` seconds. A generation ends when the whole population has died or `generationTimeLimit` runs out, and the GA produces the next generation.

### Maze mode (single start -> finish, A*-guided reward)

The `Maze1` track (built into `MazeScene` by menu item 3) isn't a closed lap — it's an open (`closedLoop: false`) corridor with right-angle turns, similar to a black-and-white maze/pipe reference image (black = wall, white = drivable corridor).

- **All cars spawn from the exact same point** (`centerline[0]`). This is safe because car-vs-car collisions are already disabled (`Physics.IgnoreLayerCollision` makes the "Car" layer not collide with itself), so stacking on top of each other on spawn causes no issue.
- **The finish point** is the last element of the centerline. When the track is open (`closedLoop == false`), `PopulationManager.Start()` builds a `GoalDistanceField`: it rasterizes the track into a grid (`MazeGrid`) and runs `AStarPathfinder.ComputeDistanceField` (a flood outward from the goal — A* with a zero heuristic, i.e. Dijkstra) **once per generation**, computing the true (wall-aware) distance from every cell to the finish. Every car then looks this up in O(1) every frame.
- **A* only shapes the reward signal — it never drives the car.** This is a deliberate design choice: the neural network + GA remain fully in control of driving; A* just tells them "how much closer to the finish did you get" (`CarAgent.ProgressFitness`) — actually learning how to drive is still entirely up to evolution.
- Reward is granted only when a car reaches a **new personal-best distance** to the goal (not on raw distance change), so wiggling back and forth near the start earns nothing — only real progress (even indirect, like going around a corner) is rewarded.
- Getting within `goalReachedDistance` (default 1.5m) of the finish sets `ReachedGoal = true` and grants a large flat bonus (+500).
- On closed-loop tracks (`WideOval`, `TightChicane`) `goalField` is never built, and `CarAgent.ComputeFitness()` automatically falls back to the original checkpoint-index-based fitness — the maze code doesn't affect the existing tracks.
- The maze's first centerline point sits 6 units in from the true open edge (`(0, 6)` rather than `(0, 0)`): the road mesh only exists *between* centerline points, so a car spawned too close to the very start has its rear half hanging off the edge with no ground under the (motor-driven) rear wheels — no traction, so it never moves no matter what the network outputs. 6 units of clearance reliably keeps the whole car body on solid road from the first frame.

### The 5 original requirements — current status

1. **Track(s):** `TrackBuilder` is fully data-driven; a `TrackDefinition` asset is just a centerline + width. The editor script creates a tight-turn track (`TightChicane`), a wide one (`WideOval`), and an open point-to-point maze (`Maze1`) — each with its own scene-builder menu item.
2. **Inspector parameters:** the `SimulationConfig` component (on the `SimulationConfig` GameObject in the scene) exposes generation count, sensor count, population size, mutation rate/strength, elitism, tournament size, and the save/load persistence settings, all from the Inspector. (Note: changing sensor count also updates `CarSensors.sensorCount`, but the network's input size needs to be set before pressing Play — live mid-run changes aren't supported yet.)
3. **HUD:** `SimulationHUD` shows the generation number, best fitness ever, how many cars are alive right now, and live diagnostics (speed, stuck timer, distance) for the current best car. `sensorRayVisuals` (an array of `LineRenderer`s) can draw the best car's sensor rays, but nothing is wired into that array by the scene builder yet — add a few `LineRenderer`s under `HUD_Canvas` and drag them onto the HUD component if you want that visualization.
4. **Vehicle physics profiles:** `VehicleProfile` (a ScriptableObject) plus 3 ready-made assets (Formula/Rally/Snowmobile) are created automatically. Changing `SimulationConfig.vehicleProfile` and pressing Play again switches the profile — a live in-game dropdown to switch profiles at runtime isn't implemented yet.
5. **Benchmark mode:** `PopulationManager.CompletionRate` (the fraction of cars that finished the track) is already computed, and `BenchmarkReportUI` compares two `PopulationManager`s side by side. Fully automating "run two configs back-to-back or in parallel" isn't implemented yet — for now you'd wire up two separate `PopulationManager` + `SimulationConfig` + track setups by hand and connect them to `BenchmarkReportUI`.

### Training persistence (save/load)

The **Persistence** section on `SimulationConfig` means training doesn't restart from scratch on every Stop/Play or Editor restart:

- At the end of every generation (`PopulationManager.FinishGeneration()`), if `saveProgress` is on (default: on), the current population's full weights + fitness, generation number, and best fitness ever are written to `Application.persistentDataPath` as `population_<track name>.json` (one file per track — use `saveSlotName` to give it a custom name if you're running multiple experiments on the same track).
- If `loadSavedProgress` is on (default: on), `PopulationManager.Start()` tries to read that file first; if it exists and its weight count per genome (which depends on `sensorCount`/`hiddenLayerSize`) still matches, it resumes the population and generation counter right where they left off. If it doesn't match (e.g. you changed the sensor count), a warning is logged and it starts fresh with a new random population — your old save file isn't deleted, just ignored.
- To start completely from scratch: delete the relevant `population_*.json` file under `Application.persistentDataPath` (on Windows, typically `%userprofile%\AppData\LocalLow\<company name>\UnityCarEvolution\`), or turn off `loadSavedProgress`.

### What's next (making it more parametric)

- Making sensor count/population/track selection choosable before Play, from an Editor window or runtime dropdowns.
- A "Car Evolution > 4. Build Benchmark Scene" menu item that automatically builds a second scene/`PopulationManager` for `TightChicane`.
- Fully automating benchmark mode: running two configs back-to-back (or in accelerated parallel via `Time.timeScale`) and showing a summary screen automatically when done.
- A simple in-Scene-view track editor (dragging centerline points around) — tracks are currently defined purely through code/assets.
- Real vehicle visuals (currently a plain box + wheel anchors) instead of a placeholder mesh/prefab.

### Notes

- Because the project uses `WheelCollider`, `com.unity.modules.vehicles` is explicitly listed in `Packages/manifest.json` — without it, `WheelCollider` won't compile.
- The "Wall" tag is created automatically by the editor script (by editing `ProjectSettings/TagManager.asset`) — you don't need to add it by hand.
- `.gitignore` includes the standard Unity ignore list, so `Library/`, `Temp/`, and similar folders won't be committed if you push this to git.

---

## Türkçe

### Genel Bakış

Her arabanın önüne yerleştirilmiş 3-7 arası raycast sensör, pist duvarlarına olan mesafeyi ölçer. Bu sensör verileri, küçük bir feedforward sinir ağına (girdi: sensör mesafeleri, çıktı: direksiyon açısı + gaz/fren) girdi olarak verilir ve arabayı bu ağ kontrol eder. Ağın ağırlıkları klasik bir genetik algoritma ile eğitilir: her nesilde bir popülasyon araba paralel olarak sürer, en iyi performans gösterenler seçilir, sonraki nesil çaprazlama + mutasyonla üretilir.

Proje ayrıca açık, tek başlangıç noktasından tek bitiş noktasına giden bir **labirent modu** içeriyor: tüm araçlar birebir aynı noktadan başlayıp tek bir bitiş noktasına ulaşmaya çalışıyor, bir A* yol bulma algoritması da fitness sinyalini hedefe doğru şekillendiriyor (A* sadece ilerlemeyi puanlıyor — arabayı asla sürmüyor; sürüşün tamamı hâlâ sinir ağı + GA'nın kontrolünde).

### Gereksinimler

- Unity Editor **6000.3.1f1** ("Unity 6.3 LTS") veya uyumlu bir Unity 6 sürümü.
- Proje `WheelCollider` kullandığı için `com.unity.modules.vehicles`'ın `Packages/manifest.json` içinde listeli kalması gerekiyor (zaten ekli).

### Kurulum

1. Unity Hub'da bu klasörü ("Add project from disk") proje olarak aç.
2. Üstteki menüden sırayla çalıştır:
   - **Car Evolution > 1. Create Data Assets (Profiles + Tracks)** — `Assets/Data/Profiles` altında Formula/Rally/Snowmobile araç profillerini, `Assets/Data/Tracks` altında WideOval / TightChicane / **Maze1** pist asset'lerini oluşturur.
   - **Car Evolution > 2. Build Simulation Scene** — `Assets/Scenes/MainScene.unity` sahnesini sıfırdan kurar: pist (WideOval), araba prefabı (`WheelCollider`'lı), `PopulationManager`, `SimulationConfig`, top-down kamera, HUD canvas.
   - **Car Evolution > 3. Build Maze Scene** — `Assets/Scenes/MazeScene.unity`'yi aynı şekilde ama `Maze1` pistiyle kurar (aşağıdaki [Labirent modu](#labirent-modu-aynı-başlangıç-noktası---bitiş-a-yönlendirmeli-ödül) bölümüne bak).
3. **Play** tuşuna bas. İlk nesil hemen başlar; HUD (sol üstte) o anki nesil numarasını, şimdiye kadarki en iyi fitness'ı, kaç aracın hâlâ hayatta olduğunu ve o anki en iyi aracın canlı istatistiklerini gösterir.

Bu üç menü öğesi asset'ler için idempotent: zaten var olan bir profil/pist asset'ini tekrar oluşturmaz. Sahne kurma işlemi ise her çalıştırıldığında ilgili `.unity` dosyasını sıfırdan yeniden kurar (o sahne üzerinde elle yaptığın değişiklikler üzerine yazılır — sahneyi yenilemek istediğinde menü öğesini tekrar çalıştırman yeterli).

### Mimari

```
Assets/Scripts/
  Sensors/CarSensors.cs          - N adet raycast atar, normalize mesafe döndürür
  NeuralNet/NeuralNetwork.cs     - girdi -> gizli(tanh) -> çıktı(tanh), flat float[] ağırlık
  GA/Genome.cs, GeneticAlgorithm.cs
                                  - elitism + turnuva seçilim + crossover + mutasyon
  Car/VehicleProfile.cs          - Formula/Rally/Snowmobile: WheelCollider parametreleri
  Car/CarController.cs           - steer + gaz/fren -> WheelCollider fiziği
  Car/CarAgent.cs                - sensör+ağ+kontrolcüyü birleştirir, fitness hesaplar
  Track/TrackDefinition.cs       - pist = 2D centerline + genişlik (data-only asset)
  Track/TrackBuilder.cs          - centerline'dan duvar + yol mesh + checkpoint üretir
  Track/CheckpointTrigger.cs
  Pathfinding/MazeGrid.cs        - centerline'dan walkable/blocked hücre grid'i
  Pathfinding/AStarPathfinder.cs - A* (FindPath) + Dijkstra-eşdeğeri toplu mesafe alanı
  Pathfinding/GoalDistanceField.cs - "bitişe olan gerçek (labirent-farkında) mesafe" - O(1) lookup
  Simulation/SimulationConfig.cs - Inspector'dan ayarlanabilir tüm parametreler
  Simulation/PopulationManager.cs - nesil döngüsü: spawn -> simüle et -> evolve
  UI/SimulationHUD.cs            - nesil/fitness metni + en iyi aracın sensör ışınları
  Benchmark/BenchmarkReportUI.cs - iki PopulationManager'ı yan yana karşılaştırır
Assets/Editor/CarEvolutionSetup.cs
                                  - tek tıkla veri asset'leri + sahne kurulumu (MainScene + MazeScene)
```

**Kapalı-tur pistlerde** (WideOval, TightChicane) fitness = kat edilen mesafe + geçilen checkpoint sayısı * bonus + hayatta kalma süresi. **Açık labirent pistinde** ise fitness, bitişe doğru gerçek A*-ölçümlü ilerlemeyle belirleniyor (bkz. [Labirent modu](#labirent-modu-aynı-başlangıç-noktası---bitiş-a-yönlendirmeli-ödül)). Her iki durumda da bir araba duvara çarptığında, pistten fiziksel olarak fırlatıldığında ya da `stuckTimeout` saniye boyunca neredeyse hareketsiz kaldığında "ölür" (`IsAlive = false`). Tüm popülasyon öldüğünde ya da `generationTimeLimit` dolduğunda nesil biter ve GA bir sonraki nesli üretir.

### Labirent modu (aynı başlangıç noktası -> bitiş, A* yönlendirmeli ödül)

`Maze1` pisti (3 numaralı menü öğesiyle `MazeScene` içine kurulur) kapalı bir tur değil — dik açılı virajlarla ilerleyen açık (`closedLoop: false`) bir koridor, siyah-beyaz bir labirent/boru görseline benzer şekilde (siyah = pist duvarı, beyaz = pistin yolu).

- **Tüm araçlar birebir aynı noktadan** (`centerline[0]`) başlıyor. Bu güvenli, çünkü araç-araç çarpışmaları zaten kapalı (`Physics.IgnoreLayerCollision` ile "Car" layer'ı kendisiyle çarpışmıyor) — üst üste spawn olmaları sorun yaratmıyor.
- **Bitiş noktası** centerline'ın son elemanı. Pist açık (`closedLoop == false`) olduğunda `PopulationManager.Start()` bir `GoalDistanceField` kuruyor: `MazeGrid` ile pisti hücrelere bölüp, `AStarPathfinder.ComputeDistanceField` (bitişten dışarı doğru çalışan, A*'ın sıfır-heuristik hali = Dijkstra) ile **her hücreden bitişe olan gerçek (duvarları dolanan) mesafeyi**, nesil başına bir kere hesaplıyor. Her araç bunu her frame'de O(1) sorguluyor.
- **A* SADECE ödül sinyalini şekillendiriyor, arabayı sürmüyor** — bilinçli bir tasarım kararı: sürüşün tamamı hâlâ sinir ağı + GA'nın kontrolünde; A* onlara sadece "bitişe ne kadar yaklaştın" diye bir puan veriyor (`CarAgent.ProgressFitness`) — sürmeyi öğrenmek tamamen evrimin işi olarak kalıyor.
- Ödül sadece **yeni bir kişisel-en-iyi mesafeye** ulaşınca veriliyor (ham mesafe değişimi değil) — böylece başlangıç noktasının yakınında ileri-geri sallanmak hiçbir ödül kazandırmıyor, gerçek ilerleme (bir köşeyi dönmek gibi dolaylı olsa bile) ödüllendiriliyor.
- Bitişe `goalReachedDistance` (varsayılan 1.5m) kadar yaklaşınca `ReachedGoal = true` olur ve büyük bir sabit bonus (+500) kazanılır.
- Kapalı-tur pistlerde (`WideOval`, `TightChicane`) `goalField` hiç kurulmaz, `CarAgent.ComputeFitness()` otomatik olarak eski checkpoint-index tabanlı fitness'a düşer — labirent kodu mevcut pistleri etkilemez.
- Labirentin ilk centerline noktası, gerçek açık uçtan 6 birim içeride duruyor (`(0, 0)` yerine `(0, 6)`): yol mesh'i sadece centerline noktaları *arasında* var, bu yüzden başlangıca çok yakın spawn olan bir aracın gövdesinin arka yarısı, altında zemin olmayan bir şekilde açık uçtan sarkar — motor torku uygulanan arka tekerlekler havada kalınca hiçbir çekiş oluşmaz ve araç, sinir ağı ne çıktı verirse versin hiç hareket etmez. 6 birimlik bir boşluk, aracın tüm gövdesinin daha ilk kareden itibaren sağlam yolun üzerinde kalmasını güvenilir şekilde sağlıyor.

### İstenen 5 madde — şu anki durum

1. **Pist(ler):** `TrackBuilder` tamamen data-driven; bir `TrackDefinition` asset'i sadece bir centerline + genişlik. Editor scripti dar-virajlı bir pist (`TightChicane`), geniş bir pist (`WideOval`) ve açık, tek başlangıç-tek bitişli bir labirent (`Maze1`) oluşturuyor — her biri kendi sahne kurma menü öğesine sahip.
2. **Inspector parametreleri:** sahnedeki `SimulationConfig` GameObject'i üzerindeki `SimulationConfig` component'i — nesil sayısı, sensör sayısı, popülasyon büyüklüğü, mutasyon oranı/gücü, elitism, turnuva boyutu ve save/load kalıcılık ayarlarının hepsi Inspector'dan ayarlanabilir. (Not: sensör sayısını değiştirmek `CarSensors.sensorCount`'u da güncelliyor, ama ağın girdi boyutu Play'e basılmadan önce ayarlanmalı — oyun sırasında canlı değiştirme henüz desteklenmiyor.)
3. **HUD:** `SimulationHUD` nesil numarasını, şimdiye kadarki en iyi fitness'ı, o an kaç aracın hayatta olduğunu ve o anki en iyi aracın canlı tanı bilgilerini (hız, sıkışma sayacı, mesafe) gösteriyor. `sensorRayVisuals` (bir `LineRenderer` dizisi) en iyi aracın sensör ışınlarını çizebilir, ama sahne kurucusu bu diziye henüz hiçbir şey bağlamıyor — istersen `HUD_Canvas` altına birkaç `LineRenderer` ekleyip HUD component'ine sürükleyerek bu görselleştirmeyi aktif edebilirsin.
4. **Araç fizik profilleri:** `VehicleProfile` (bir ScriptableObject) ve 3 hazır asset (Formula/Rally/Snowmobile) otomatik oluşturuluyor. `SimulationConfig.vehicleProfile` alanını değiştirip Play'e tekrar basmak profili değiştirir — çalışma zamanında canlı bir dropdown ile geçiş henüz uygulanmadı.
5. **Benchmark modu:** `PopulationManager.CompletionRate` (pisti bitiren araçların oranı) zaten hesaplanıyor, `BenchmarkReportUI` iki `PopulationManager`'ı yan yana karşılaştırıp raporluyor. "İki config'i art arda veya paralel çalıştırmayı" tamamen otomatik hale getirmek henüz yapılmadı — şu an için iki ayrı `PopulationManager` + `SimulationConfig` + pist kurulumunu elle yapıp `BenchmarkReportUI`'a bağlaman gerekiyor.

### Eğitimin kalıcı hale getirilmesi (Save/Load)

`SimulationConfig` üzerindeki **Persistence** bölümü sayesinde eğitim her Stop/Play'de veya Editor'ü kapatıp açtığında sıfırdan başlamıyor:

- Her nesil bitiminde (`PopulationManager.FinishGeneration()`), `saveProgress` açıksa (varsayılan: açık) o anki popülasyonun tüm ağırlıkları + fitness'ları, nesil numarası ve şimdiye kadarki en iyi fitness `Application.persistentDataPath` altına `population_<pist adı>.json` olarak yazılır (pist başına ayrı dosya — aynı pistte birden fazla deney yürütüyorsan `saveSlotName` alanıyla özel bir isim verebilirsin).
- `loadSavedProgress` açıksa (varsayılan: açık), `PopulationManager.Start()` önce bu dosyayı okumayı dener; varsa ve genom başına ağırlık sayısı (`sensorCount`/`hiddenLayerSize`'a bağlı) hâlâ uyuşuyorsa, popülasyonu ve nesil sayacını kaldığı yerden devam ettirir. Uyuşmuyorsa (örneğin sensör sayısını değiştirdiysen) bir uyarı loglar ve rastgele yeni bir popülasyonla baştan başlar — eski dosyan silinmez, sadece görmezden gelinir.
- Sıfırdan başlamak istersen: `Application.persistentDataPath` klasöründeki ilgili `population_*.json` dosyasını sil (Windows'ta genelde `%userprofile%\AppData\LocalLow\<şirket adı>\UnityCarEvolution\`), ya da `loadSavedProgress`'i kapat.

### Sırada ne var (parametrik hale getirme)

- Sensör sayısı/popülasyon/pist seçimini bir Editor penceresinden veya runtime dropdown'lardan Play öncesi seçilebilir hale getirmek.
- `TightChicane` için otomatik ikinci bir sahne/`PopulationManager` kuran bir "Car Evolution > 4. Build Benchmark Scene" menü öğesi.
- Benchmark modunu tamamen otomatik hale getirmek: iki config'i art arda (veya `Time.timeScale` ile hızlandırılmış paralel) çalıştırıp bitince özet ekranını otomatik göstermek.
- Basit bir Scene-view pist editörü (centerline noktalarını sürükleyip bırakma) — şu an pistler sadece kod/asset üzerinden tanımlanıyor.
- Gerçek araç görselleri (şu an sade bir kutu + tekerlek anchor'ları) — yer tutucu mesh/prefab yerine gerçek bir modelle değiştirmek.

### Notlar

- Proje `WheelCollider` kullandığı için `com.unity.modules.vehicles`, `Packages/manifest.json` içinde açıkça listelendi — bu modül olmadan `WheelCollider` derlenmez.
- "Wall" tag'i editor scripti tarafından otomatik oluşturuluyor (`ProjectSettings/TagManager.asset` düzenlenerek), elle eklemene gerek yok.
- `.gitignore` standart Unity ignore listesini içeriyor — bu projeyi git'e/GitHub'a bağlarsan `Library/`, `Temp/` gibi klasörler commit'lenmez.
