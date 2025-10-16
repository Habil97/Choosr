# Choosr

ASP.NET Core MVC tabanlı interaktif seçim ("o mu bu mu", turnuva, sıralama) quiz platformu.

## Katmanlar
- **Choosr.Domain**: İleride eklenecek çekirdek domain modelleri ve kurallar.
- **Choosr.Infrastructure**: Veri erişimi (şimdilik boş). MS SQL + EF Core eklenecek.
- **Choosr.Web**: MVC katmanı, ViewModel, ViewComponent, Partial ve statik dosyalar.

## Geçici Veri
`InMemoryQuizService` arayüz ve sahte veri sağlar. Backend tamamlandığında yerine repository + EF Core gelecek.

## Frontend Özeti
- Koyu tema (`wwwroot/css/theme.css`)
- Partial'lar: Header, Footer, QuizCard, CategorySelect, Pagination
- ViewComponent'ler: EditorPicks, Trending, Latest, Popular
- Quiz oluşturma sihirbazı: 3 adım (Detaylar, Seçimler, Sonuç)
- Oynama modları (mock): VS, Bracket, Rank

## Yol Haritası (Backend sonrası)
1. Domain modelleri: Quiz, Choice, User, Reaction, Comment, PlaySession.
2. EF Core konfigürasyonları ve migration'lar.
3. Kimlik: ASP.NET Core Identity entegrasyonu.
4. Gerçek dosya yükleme (görsel) + YouTube link doğrulama.
5. Oynama algoritmaları ve gerçek zamanlı istatistik güncelleme.
6. Önbellekleme (MemoryCache / Redis).

## Geliştirme
```
dotnet build
dotnet run --project Choosr.Web
```

## Önbellekleme Stratejileri
- Output Caching: Ana sayfa ve listeleme sayfalarında 20–60 sn arası sayfa seviyesinde önbellek.
- IMemoryCache: Popüler/Trend/Latest kart listeleri kısa TTL ile saklanır.
- Tag istatistikleri: `TagStatsService` sonuçları (en popüler etiketler, eş-oluşumlar) 20 dk TTL ile bellekte tutulur. Veri değişikliği (quiz ekleme/güncelleme/silme) olduğunda `TagStatsInvalidator` versiyon anahtarını artırır; böylece tüm tag istatistik cache anahtarları otomatik olarak geçersiz kalır.

## Not
Bu aşamada güvenlik, yetkilendirme ve veri doğrulama minimum seviyededir; MVP arayüz akışını göstermek içindir.