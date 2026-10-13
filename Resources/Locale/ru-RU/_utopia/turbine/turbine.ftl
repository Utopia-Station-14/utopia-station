turbine-console-window-title = Консоль управления турбинами
turbine-console-window-header-title = Панель управления турбинами
turbine-console-window-header-turbines = Список активных турбин:

turbine-console-window-no-turbine-selected = Турбина не выбрана
turbine-console-window-turbine-name = Турбина { $netEntity }

turbine-console-window-label-rpm = Оборотов в минуту:
turbine-console-window-value-rpm = { $current } / { $max } ОвМ
turbine-console-window-label-pressure = Давление:
turbine-console-window-value-pressure = { $current } / { $max } КПа
turbine-console-window-label-temp = Температура:
turbine-console-window-value-temp = { $current } / { $max } K
turbine-console-window-label-energy = Выработка электроенергии:
turbine-console-window-value-energy = { $energy } КВт
turbine-console-window-label-integrity = Целостность турбины:
turbine-console-window-value-integrity = { $integrity }%

turbine-console-window-button-enable = Включить
turbine-console-window-button-disable = Выключить

turbine-console-window-status-off = ВЫКЛ
turbine-console-window-status-nominal = НОРМА
turbine-console-window-status-warning = ВНИМАНИЕ
turbine-console-window-status-critical = КРИТИЧНО

turbine-console-window-tag-off = ВЫКЛ
turbine-console-window-tag-nominal = ОК
turbine-console-window-tag-warning = ВНИМ
turbine-console-window-tag-critical = КРИТ

turbine-monitoring-window-flavor-left = Made by M&Team production
turbine-monitoring-window-flavor-right = V1.0
turbine-console-window-value-none = -

turbine-heat-damage = ВНИМАНИЕ: Критический перегрев турбины! Опасность разрушения!
turbine-pressure-damage = ВНИМАНИЕ: Избыточное давление в турбине! Опасность разрушения!
turbine-energy-damage = ВНИМАНИЕ: Превышение ОвМ! Целостность турбины под угрозой!
turbine-pashalka-damage = ВНИМАНИЕ: Зафиксирован критический износ градиентного кожуха энтропийного преобразователя турбины! Аварийно-предохранительный клапан для непредвиденных ситуаций не может быть открыт из-за непредвиденной ситуации в протоколе непредвиденных ситуаций!

ent-TurbineRotor = ротор Турбины
    .desc = Тяжёлый вращающийся вал с лопатками, преобразующий энергию потока газа в механическую работу.
ent-TurbineExhaust = выхлоп Турбины
    .desc = Выпускное отверстие, через которое отработанные газы выводятся наружу.
ent-TurbineInlet = компрессор Турбины
    .desc = Впускное устройство, нагнетающее газ в ротор турбины.
ent-ComputerTurbine = консоль управления Турбинами
    .desc = Интерфейс для мониторинга состояния промышленных турбин.