# PitWall

iRacing için gerçek zamanlı telemetri paneli ve tur karşılaştırma aracı. WPF (.NET 10) masaüstü uygulaması.

## Ne yapar

- iRacing simülatörünün paylaşımlı bellek (shared memory) arayüzünden canlı telemetri okur — Hız, RPM, vites, gaz/fren, direksiyon torku (FFB), tur zamanları, yakıt.
- Canlı grafiklerle gaz/fren, direksiyon torku ve hızı zaman içinde gösterir.
- Tamamlanan her turu SQLite veritabanına kaydeder (tur mesafesine göre örneklenmiş hız/gaz/fren/RPM verisiyle birlikte).
- Kayıtlı iki turu seçip hız-mesafe grafiği üzerinde karşılaştırma yapar.

> **Not:** iRacing, rFactor/ACC gibi UDP telemetri yayını yapmaz — verilerini bir memory-mapped file üzerinden sunar. Bu yüzden UDP soket yerine [IRSDKSharper](https://github.com/mherbold/IRSDKSharper) kütüphanesi kullanılıyor.

## Gereksinimler

- Windows, .NET 10 SDK
- iRacing simülatöründe paylaşımlı bellek telemetrisi açık olmalı — `Documents/iRacing/app.ini` dosyasında:
  ```
  irsdkEnableMem=1
  ```

## Çalıştırma

```bash
dotnet run --project src/PitWall/PitWall.csproj
```

Uygulama iRacing simülatörü kapalıyken de açılır; bağlantı durumu üst çubukta gösterilir ve sim başladığında otomatik bağlanır.

## Veri

Turlar `%LOCALAPPDATA%\PitWall\pitwall.db` (SQLite) içinde saklanır — `Sessions`, `Laps`, `LapSamples` tabloları.

## Teknoloji

- IRSDKSharper — iRacing shared-memory telemetri erişimi
- LiveChartsCore.SkiaSharpView.WPF — canlı grafikler
- Microsoft.Data.Sqlite — tur kayıtları

## Durum

Faz 1: canlı gösterge paneli + tur kaydı + iki tur karşılaştırma tamamlandı. Bu geliştirme ortamında iRacing kurulu olmadığı için canlı sim bağlantısı test edilemedi — uygulama "bağlı değil" durumuyla hatasız açılıp kapanıyor; gerçek sim ile ilk bağlantıda küçük düzeltmeler gerekebilir.
