# YtLight — Minimalist Native YouTube Music Desktop Client

<p align="center">
  <b>Windows 10 ve 11 için Ultra Düşük RAM Tüketen, Hızlı ve Sade YouTube Music Masaüstü Uygulaması</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-blue?logo=windows" alt="Platform">
  <img src="https://img.shields.io/badge/.NET-8.0%20WPF-purple?logo=dotnet" alt=".NET 8">
  <img src="https://img.shields.io/badge/WebView2-Native-darkgreen?logo=microsoftedge" alt="WebView2">
  <img src="https://img.shields.io/badge/Version-v1.0.0-red" alt="Version 1.0.0">
  <img src="https://img.shields.io/badge/RAM-~50--110%20MB-success" alt="Ultra Low RAM">
  <img src="https://img.shields.io/badge/License-MIT-green" alt="MIT License">
</p>

---

## 🎯 Proje Amacı ve Felsefesi

**YtLight**, resmi web sürümünün ve Electron tabanlı masaüstü uygulamalarının yüksek bellek tüketiminden (500 MB – 1.5 GB RAM) ve görsel ağırlığından arındırılmış, tamamen performans ve sadelik odaklı geliştirilmiş yerel (native) bir Windows masaüstü oynatıcısıdır.

- **Boşta RAM:** ~50 MB
- **Müzik Çalarken RAM:** ~80 – 110 MB
- **Sistem Tepsisine (System Tray) Küçültüldüğünde:** Bellek agresif olarak temizlenir (`MemoryGuardian`), UI render maliyeti sıfıra iner.
- **Sıfır Görsel Kalabalık:** Spektrum analizörleri, video arka planları veya ağır efektler yoktur. Sadece temiz tipografi, küçük albüm kapakları ve şık koyu tema bulunur.

---

## ✨ Başlıca Özellikler

### 1. Kimlik Doğrulama & Marka / Kanal Hesap İzolasyonu
- **Güvenli WebView2 Girişi:** Google hesabınızla tek seferlik güvenli giriş yapılır. Oturum çerezleri (`SAPISID`, `SID`, `__Secure-3PAPISID`) Windows DPAPI ile şifrelenerek yerel ortamda korunur.
- **Çoklu Profil / Kanal Desteği (Brand Accounts):** `account/accounts_list` uç noktası taranarak kullanıcının tüm YouTube kanalları listelenir. Üst bardaki kanal menüsünden geçiş yapıldığında `X-Goog-PageId` başlığı otomatik ayarlanır; yalnızca seçili kanala ait kütüphane ve çalma listeleri gösterilir.
- **Tekil WebView Ortamı:** Çerezler ve oynatıcı belleği optimize edilmiş tek bir `CoreWebView2Environment` üzerinden yönetilerek çökmeler ve çakışmalar önlenir.

### 2. Tam Kütüphane ve Geçmiş Senkronizasyonu
- **Resmi YouTube Music Geçmişi:** Çalınan her parça için `videostatsPlaybackUrl` ve `videostatsWatchtimeUrl` bildirimleri periyodik olarak gönderilir; YouTube Music öneri algoritmanız ve dinleme geçmişiniz anında beslenir.
- **Sayfalamalı Çalma Listesi Yükleme (Pagination):** Devam belirteçleri (`continuationToken`) otomatik takip edilerek 1000+ şarkılık devasa listelerin tamamı eksiksiz yüklenir.
- **Kütüphane Yönetimi:** Çalma listesi oluşturma, listeye şarkı ekleme/çıkarma, Like/Dislike işlemleri doğrudan bulut hesabınızla senkronizedir.

### 3. Gelişmiş Müzik Çalar (Player)
- **Hızlı ve Kesintisiz Geçiş:** Sayfa baştan yüklenmeden, doğrudan YouTube oynatıcı motoru üzerinden anlık şarkı değişimi (`loadVideoById`).
- **Ergonomik Kontroller:**
  - Ortada büyük dairesel Oynat/Duraklat (`CirclePlayButton`) butonu.
  - Solunda Karışık Çal (`🔀`) ve Önceki Şarkı (`⏮`).
  - Sağında Sonraki Şarkı (`⏭`), Tekrar Modu (`🔁` Liste / `🔂` Tek Şarkı / Kapalı), Beğen (`❤`) ve Beğenme (`👎`).
- **Hassas Ses Denetimi:** Doğal işitme eğrisine uygun logaritmik ses skalası (`LoudnessNormalizer`) ve sessize alma (`Mute`).
- **Gelişmiş İlerleme Çubuğu:** Sürükle-bırak destekli, milisaniyelik hassasiyete sahip seek bar.
- **Uyku Modu (Sleep Timer):** 15, 30, 45, 60 dakika veya "Şarkı Bittiğinde" sesi yavaşça kısarak (fade-out) durduran ve isteğe bağlı olarak uygulamayı tamamen kapatan zamanlayıcı.
- **Windows SMTC & Medya Tuşları:** Klavyedeki ses ve şarkı değiştirme tuşlarıyla tam uyumluluk (System Media Transport Controls).
- **Sistem Tepsisi (Tray):** Arka planda kesintisiz çalma, tepsi sağ tık menüsünden oynatma kontrolleri ve uyku modu.

---

## 🏗️ Proje Mimarisi

Çözüm temiz mimari (Clean Architecture) ve MVVM prensiplerine göre modüler olarak yapılandırılmıştır:

```
ytlight/
├── src/
│   ├── YtLight.Core/            # Paylaşılan veri modelleri, arayüzler ve kontratlar
│   ├── YtLight.InnerTube/       # YouTube InnerTube API istemcisi, Auth, Sapisid hash ve parser'lar
│   ├── YtLight.Playback/        # Ses motoru, SMTC servisi, ses normalizasyonu ve kuyruk yönetimi
│   └── YtLight.App/             # WPF arayüzü, MVVM View & ViewModel katmanı, WebView2 oynatıcı
├── Directory.Build.props        # Genel derleme ve dil sürümü ayarları
├── YtLight.slnx                 # Çözüm dosyası
├── baslat.bat                   # Hızlı başlatma betiği
└── .gitignore                   # Kapsamlı .NET / Visual Studio yoksayma kuralları
```

---

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler
- **İşletim Sistemi:** Windows 10 (Sürüm 1809 / Build 17763 veya üstü) ya da Windows 11
- **Çalışma Zamanı (Runtime):** [.NET 8.0 SDK / Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Bileşen:** [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (Windows 11 ve güncel Windows 10 sürümlerinde varsayılan olarak yüklüdür)

### Kaynak Koddan Derleme

1. Depoyu klonlayın:
   ```bash
   git clone https://github.com/<KULLANICI_ADINIZ>/ytlight.git
   cd ytlight
   ```

2. Projeyi derleyin:
   ```powershell
   dotnet build src\YtLight.App\YtLight.App.csproj -c Release
   ```

3. Uygulamayı çalıştırın:
   ```powershell
   dotnet run --project src\YtLight.App
   ```
   *Veya `src\YtLight.App\bin\Release\net8.0-windows10.0.17763.0\YtLight.exe` dosyasını doğrudan açabilirsiniz.*

---

## ⌨️ Kısayollar ve Kullanım

| Eylem | Kısayol / Etkileşim |
|---|---|
| Oynat / Duraklat | Klavyedeki Medya Play tuşu veya Ortadaki Büyük Buton |
| Sonraki / Önceki Şarkı | Klavyedeki Medya tuşları veya `⏮` / `⏭` butonları |
| Hızlı Arama | Üst arama çubuğuna yazıp `Enter` tuşuna basın, `Esc` ile kapatın |
| Sistem Tepsisine Küçültme | Pencereyi küçült simgesine basıldığında tepsiye iner |
| Tepsiden Geri Getirme | Tepsi simgesine çift tıklayın |

---

## 📄 Lisans

Bu proje [MIT Lisansı](LICENSE) kapsamında lisanslanmıştır. Detaylar için `LICENSE` dosyasına göz atabilirsiniz.
