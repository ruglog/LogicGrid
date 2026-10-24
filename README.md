# LogicGrid

LogicGrid is a desktop application built on the Avalonia UI framework, designed for creating, editing, and visualizing flowcharts and tree structures. / LogicGrid — это настольное приложение, разработанное на фреймворке Avalonia UI, которое предназначено для создания, редактирования и визуализации блок-схем и древовидных структур.

---

## 🇬🇧 English

### Features

#### 🧩 Display Modes
* **Mode toggling:** Switch between "Schema" and "Tree" modes via a dropdown menu or using hotkeys (`Ctrl + X` / `Ctrl + Shift + X`).
* **Adaptive layout:** Automatic reorganization and adaptation of connecting lines based on the active mode.

#### 📦 Node & Text Management
* **Root blocks:** Create root blocks using a dedicated control button.
* **Child nodes:** Add child nodes via the element's context menu.
* **In-app editor:** Built-in multi-line text editor (TextBox) inside each block.
* **Color palettes:** Choose between black and white block styling themes.
* **Size adjustment:** Configure dimensions (width and height) via control panels or by interactive resizing (`Ctrl + Left Mouse Button`).

#### 🔗 Element Connections
* **Bezier curves:** Smooth rendering of Bezier curves connecting parent and child nodes.
* **Dynamic tracking:** Real-time updates to line coordinates whenever connected blocks are moved or resized.

#### 🗺️ Canvas Navigation & Interactivity
* **Panning:** Free viewport navigation by dragging across the empty canvas.
* **Zooming:** Smooth mouse-wheel zooming (while holding `Ctrl`) within a 20% to 400% range.
* **Quick centering:** Dedicated button to immediately center the entire diagram on the screen.
* **Interactive background:** Animated background that visually reacts to cursor movement and block positions.

#### 💾 File & Data Management
* **Save to JSON:** Export the created layout to a JSON file (`Ctrl + S`).
* **Load from JSON:** Open and load layouts from JSON files (`Ctrl + O`).
* **Resolution protection:** Built-in warning system when opening files created on different screen resolutions to prevent elements from going off-screen.
* **Canvas reset:** "Clear All" function to completely wipe and reset the canvas.

#### ↩️ History & Clipboard
* **Multi-level Undo:** Comprehensive undo system (`Ctrl + Z`) covering moves, resizing, text edits, and graph structure modifications.
* **Clipboard actions:** Copy and paste independent blocks (`Ctrl + C` / `Ctrl + V`).
* **Advanced copying:** Copy blocks along with their entire child hierarchy (`Ctrl + Shift + C` / `Ctrl + Shift + V`).

#### 📖 Built-in Help
* **Help reference:** Interactive modal window providing a comprehensive guide to hotkeys and control elements, accessible by pressing `F1` or via the top menu bar.

### Installation
1. Download the installer
2. Run the installer
3. Follow the setup wizard steps
4. Launch LogicGrid

---

## 🇷🇺 Русский

### Функционал

#### 🧩 Режимы отображения
* **Переключение режимов:** Смена режимов «Схема» и «Древо» через выпадающий список в интерфейсе либо с помощью горячих клавиш (`Ctrl + X` / `Ctrl + Shift + X`).
* **Адаптивная разметка:** Автоматическая перестройка и адаптация соединительных линий в зависимости от активного режима.

#### 📦 Работа с узлами и текстом
* **Корневые блоки:** Создание корневых элементов с помощью специальной кнопки управления.
* **Дочерние узлы:** Добавление зависимых элементов через контекстное меню блока.
* **Встроенный редактор:** Многострочный текстовый редактор (TextBox) внутри каждого блока.
* **Цветовые палитры:** Выбор черного или белого варианта оформления блоков.
* **Настройка габаритов:** Изменение ширины и высоты через панели управления или интерактивный ресайз (`Ctrl + Левая кнопка мыши`).

#### 🔗 Связи между элементами
* **Кривые Безье:** Плавная отрисовка линий Безье, соединяющих родительские и дочерние узлы.
* **Динамический трекинг:** Обновление координат линий в реальном времени при перемещении или изменении размеров связанных блоков.

#### 🗺️ Навигация и интерактивность холста
* **Панорамирование:** Свободное перемещение по рабочей области путем перетаскивания мыши по пустому полю холста.
* **Масштабирование:** Плавный зум колесиком мыши (при зажатой клавише `Ctrl`) в диапазоне от 20% до 400%.
* **Быстрое центрирование:** Выделенная кнопка для мгновенного выравнивания всей схемы по центру экрана.
* **Интерактивный фон:** Анимированный бэкграунд, который визуально реагирует на движение курсора мыши и расположение блоков.

#### 💾 Управление файлами и данными
* **Сохранение в JSON:** Экспорт созданной схемы в файл JSON (`Ctrl + S`).
* **Загрузка из JSON:** Открытие и импорт готовых схем из файлов JSON (`Ctrl + O`).
* **Защита разрешения:** Система предупреждений при открытии файлов, созданных на другом разрешении экрана (защита элементов от выхода за пределы видимости).
* **Сброс холста:** Функция «Очистить всё» для полной очистки рабочей области.

#### ↩️ История и буфер обмена
* **Многоуровневый Undo:** Система отмены последних действий (`Ctrl + Z`), охватывающая перемещения, изменение размеров, редактирование текста и структуру графа.
* **Работа с буфером:** Копирование и вставка одиночных независимых блоков (`Ctrl + C` / `Ctrl + V`).
* **Расширенное копирование:** Копирование блоков вместе со всей их дочерней иерархией (`Ctrl + Shift + C` / `Ctrl + Shift + V`).

#### 📖 Встроенная справка
* **Интерактивный справочник:** Модальное окно с полным руководством по горячим клавишам и элементам управления, доступное по нажатию `F1` или через верхнее меню программы.

### Установка
1. Скачать установщик
2. Запустить установщик
3. Выполнить шаги мастера настройки
4. Запустить LogicGrid
