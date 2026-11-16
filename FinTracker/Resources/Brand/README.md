# ФинТрек — логотип

## Идея
Тёмная matte-подложка, золотая монета и знак доллара. Монета — деньги и
накопления, `$` — валюта и бюджет. Фиолетовый остался акцентом словесного
знака и свечения приложения.

## Файлы
| Файл | Назначение |
| --- | --- |
| `fintrack-logo-dark.svg` | Основной логотип для тёмного фона (знак + «ФинТрек» + «Личный бюджет») |
| `fintrack-logo-light.svg` | То же для светлого фона |
| `fintrack-mark.svg` | Тёмный знак с монетой (для сайта, презентаций, документов) |
| `fintrack-mark-flat.svg` | Копия знака в сплошных цветах — безопасна для MAUI resizetizer |
| `fintrack-glyph.svg` | Только золотая монета со знаком доллара |
| `fintrack-coin-badge.svg` | Монета с плотной обрезкой — для шапки приложения |
| `fintrack-icon.svg` | Полная иконка приложения 456×456 |
| `fintrack-icon-background.svg` | Тёмный фон адаптивной иконки Android |
| `fintrack-icon-foreground.svg` | Передний план адаптивной иконки (безопасная зона) |
| `fintrack-splash.svg` | Заставка (фон задаётся `MauiSplashScreen`, `#121212`) |
| `preview.html` | Превью всех версий |
| `fintrack-logo-dark.png`, `fintrack-logo-light.png` | Растровые версии 2× |
| `fintrack-mark-512.png`, `fintrack-icon-512.png`, `fintrack-coin-512.png` | Растровые версии знака, иконки и монеты |

## Палитра
- Тёмный интерфейс: `#0F0F11`, карточки `#17171A`, знак `#17171B`
- Золото: `#F6C453`, тень `#DFA22A`
- Акцент словесного знака: `#B9A3FF` (на тёмном), `#6D28D9` (на светлом)
- Белый: `#FFFFFF`

## Где уже применено
- `Resources/AppIcon/appicon.svg` и `appiconfg.svg` — иконка приложения.
- `Resources/Splash/splash.svg` — заставка.
- `Resources/Images/fintrack_mark.svg` — монета в шапке `MainPage` и в карточке «О приложении» в настройках.
- `Resources/Images/fintrack_logo.svg` — полный логотип на экране входа `LoginPage`.
- `MainPage.xaml` — шапка приложения и карточка «О приложении» в настройках.
- `LoginPage.xaml` — экран входа в тёмном оформлении.

Старые версии сохранены рядом:
`appicon.wallet.svg.bak`, `appiconfg.wallet.svg.bak`, `splash.wallet.svg.bak`.

## Шрифт
Надпись «ФинТрек» переведена в кривые на основе Inter (SIL OFL).
В самом приложении остаются штатные шрифты Inter.
