--file-log: путь к файлу журнала доступа.
--file-output: путь к файлу, в который будут записаны результаты.
--address-start: начальный адрес для фильтрации.
--address-mask: маска адреса для фильтрации.
--time-start: начальное время интервала.
--time-end: конечное время интервала.

Пример запуска
dotnet <Project> --file-log "C:\logs\logfile.txt" --file-output "C:\output\outputfile.txt" --address-start "192.168.1.1" --address-mask "255.255.255.0" --time-start "01.04.2024" --time-end "09.04.2024"
