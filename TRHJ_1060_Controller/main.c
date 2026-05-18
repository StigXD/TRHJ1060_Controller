/* USER CODE BEGIN Header */
/**
  ******************************************************************************
  * @file           : main.c
  * @brief          : Main program body
  ******************************************************************************
  * @attention
  *
  * Copyright (c) 2025 STMicroelectronics.
  * All rights reserved.
  *
  * This software is licensed under terms that can be found in the LICENSE file
  * in the root directory of this software component.
  * If no LICENSE file comes with this software, it is provided AS-IS.
  *
  ******************************************************************************
  */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include "main.h"
#include "usb_device.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */
#include <string.h>
#include <stdlib.h>
/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */

/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */

/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */

/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/
SPI_HandleTypeDef hspi1;

TIM_HandleTypeDef htim1;

/* USER CODE BEGIN PV */
extern USBD_HandleTypeDef hUsbDeviceFS;

// Определение пинов
#define CS_PIN GPIO_PIN_4
#define CS_PORT GPIOA
#define LATCH_PIN GPIO_PIN_3
#define LATCH_PORT GPIOA
#define RESET_PIN GPIO_PIN_2
#define RESET_PORT GPIOA

// Буферы для USB
#define USB_RX_BUFFER_SIZE 64  // 6 символов + \n + \0
uint8_t usb_rx_buffer[USB_RX_BUFFER_SIZE];
uint8_t usb_rx_index = 0;
uint8_t usb_cmd_buffer[3];
uint8_t usb_cmd_ready = 0;
uint8_t data;


// Контрольные регистры
uint32_t controlRegister[25] =
  {
      0x030020, 0x030120, 0x030220, 0x030326, 0x03041E,
	  0x030820, 0x030920, 0X030A20, 0x030B26, 0x030C1E,
	  0x031020, 0x031120, 0X031220, 0x031326, 0x03141E,
	  0x031820, 0x031920, 0x031A20, 0x031B26, 0x031C1E,
	  0x03F75A, 0x03FA5A, 0x03FB5A, 0x03FE5A, 0x03F65A
  };
/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
void SystemClock_Config(void);
static void MX_GPIO_Init(void);
static void MX_SPI1_Init(void);
static void MX_TIM1_Init(void);

/* USER CODE BEGIN PFP */
void delay_us(uint16_t us);
void transmit_24bit(uint32_t data);
void process_usb_command(uint8_t *command);
void reset_chip(void);
void init_control_registers(void);
void send_response(const char *response);
void process_commands(void);
static void term_send(const char *s);
static uint8_t hex2nibble(char c);
static uint8_t hex2byte(const char *p);

// Задержка в микросекундах (базисная реализация)
void delay_us(uint16_t us)
{
    __HAL_TIM_SET_COUNTER(&htim1, 0);
    while (__HAL_TIM_GET_COUNTER(&htim1) < us);
}

// ДОБАВИТЬ прототипы для TRHJ-1060 функций:
uint32_t* TRHJ1060_PrepareTemperatureReading(uint8_t chipId);
uint32_t TRHJ1060_ReadSignalRegister(uint8_t chipId, uint8_t address);
float TRHJ1060_ParseTemperatureResponse(uint8_t* response);
/* USER CODE END PFP */

/* Private user code ---------------------------------------------------------*/
/* USER CODE BEGIN 0 */

/* USER CODE END 0 */

/**
  * @brief  The application entry point.
  * @retval int
  */
int main(void)
{

  /* USER CODE BEGIN 1 */

  /* USER CODE END 1 */

  /* MCU Configuration--------------------------------------------------------*/

  /* Reset of all peripherals, Initializes the Flash interface and the Systick. */
  HAL_Init();

  /* USER CODE BEGIN Init */

  /* USER CODE END Init */

  /* Configure the system clock */
  SystemClock_Config();

  /* USER CODE BEGIN SysInit */

  /* USER CODE END SysInit */

  /* Initialize all configured peripherals */
  MX_GPIO_Init();
  MX_SPI1_Init();
  MX_TIM1_Init();
  MX_USB_DEVICE_Init();
  /* USER CODE BEGIN 2 */
  // Небольшая задержка перед инициализацией USB
  HAL_Delay(100);

  // Включение таймера для задержек
  HAL_TIM_Base_Start(&htim1);

  // Инициализация сброса
  //reset_chip();

  // Инициализация USB приема
  //USBD_CDC_SetRxBuffer(&hUsbDeviceFS, usb_rx_buffer);
  //USBD_CDC_ReceivePacket(&hUsbDeviceFS);

  // Отладочное сообщение
  //HAL_Delay(100);
  //send_response("USB Device Ready");

  /* USER CODE END 2 */

  /* Infinite loop */
  /* USER CODE BEGIN WHILE */
  while (1)
  {
	  process_commands();
    /* USER CODE END WHILE */

    /* USER CODE BEGIN 3 */
  }
  /* USER CODE END 3 */
}

/**
  * @brief System Clock Configuration
  * @retval None
  */
void SystemClock_Config(void)
{
  RCC_OscInitTypeDef RCC_OscInitStruct = {0};
  RCC_ClkInitTypeDef RCC_ClkInitStruct = {0};
  RCC_PeriphCLKInitTypeDef PeriphClkInit = {0};

  /** Initializes the RCC Oscillators according to the specified parameters
  * in the RCC_OscInitTypeDef structure.
  */
  RCC_OscInitStruct.OscillatorType = RCC_OSCILLATORTYPE_HSE;
  RCC_OscInitStruct.HSEState = RCC_HSE_ON;
  RCC_OscInitStruct.HSEPredivValue = RCC_HSE_PREDIV_DIV1;
  RCC_OscInitStruct.HSIState = RCC_HSI_ON;
  RCC_OscInitStruct.PLL.PLLState = RCC_PLL_ON;
  RCC_OscInitStruct.PLL.PLLSource = RCC_PLLSOURCE_HSE;
  RCC_OscInitStruct.PLL.PLLMUL = RCC_PLL_MUL9; // 8MHz * 9 = 72MHz
  if (HAL_RCC_OscConfig(&RCC_OscInitStruct) != HAL_OK)
  {
    Error_Handler();
  }

  /** Initializes the CPU, AHB and APB buses clocks
  */
  RCC_ClkInitStruct.ClockType = RCC_CLOCKTYPE_HCLK|RCC_CLOCKTYPE_SYSCLK
                              |RCC_CLOCKTYPE_PCLK1|RCC_CLOCKTYPE_PCLK2;
  RCC_ClkInitStruct.SYSCLKSource = RCC_SYSCLKSOURCE_PLLCLK;
  RCC_ClkInitStruct.AHBCLKDivider = RCC_SYSCLK_DIV1;     // HCLK = 72MHz
  RCC_ClkInitStruct.APB1CLKDivider = RCC_HCLK_DIV2;      // PCLK1 = 36MHz
  RCC_ClkInitStruct.APB2CLKDivider = RCC_HCLK_DIV1;      // PCLK2 = 72MHz

  if (HAL_RCC_ClockConfig(&RCC_ClkInitStruct, FLASH_LATENCY_2) != HAL_OK)
  {
    Error_Handler();
  }

  /** Configure USB clock
  */
  PeriphClkInit.PeriphClockSelection = RCC_PERIPHCLK_USB;
  PeriphClkInit.UsbClockSelection = RCC_USBCLKSOURCE_PLL_DIV1_5; // 72MHz / 1.5 = 48MHz
  if (HAL_RCCEx_PeriphCLKConfig(&PeriphClkInit) != HAL_OK)
  {
    Error_Handler();
  }
}
/**
  * @brief SPI1 Initialization Function
  * @param None
  * @retval None
  */
static void MX_SPI1_Init(void)
{

  /* USER CODE BEGIN SPI1_Init 0 */

  /* USER CODE END SPI1_Init 0 */

  /* USER CODE BEGIN SPI1_Init 1 */

  /* USER CODE END SPI1_Init 1 */
  /* SPI1 parameter configuration*/
  hspi1.Instance = SPI1;
  hspi1.Init.Mode = SPI_MODE_MASTER;
  hspi1.Init.Direction = SPI_DIRECTION_2LINES;
  hspi1.Init.DataSize = SPI_DATASIZE_8BIT;
  hspi1.Init.CLKPolarity = SPI_POLARITY_LOW;
  hspi1.Init.CLKPhase = SPI_PHASE_1EDGE;
  hspi1.Init.NSS = SPI_NSS_SOFT;
  hspi1.Init.BaudRatePrescaler = SPI_BAUDRATEPRESCALER_256;
  hspi1.Init.FirstBit = SPI_FIRSTBIT_MSB;
  hspi1.Init.TIMode = SPI_TIMODE_DISABLE;
  hspi1.Init.CRCCalculation = SPI_CRCCALCULATION_DISABLE;
  hspi1.Init.CRCPolynomial = 10;
  if (HAL_SPI_Init(&hspi1) != HAL_OK)
  {
    Error_Handler();
  }
  /* USER CODE BEGIN SPI1_Init 2 */

  /* USER CODE END SPI1_Init 2 */

}

/**
  * @brief TIM1 Initialization Function
  * @param None
  * @retval None
  */
static void MX_TIM1_Init(void)
{

  /* USER CODE BEGIN TIM1_Init 0 */

  /* USER CODE END TIM1_Init 0 */

  //TIM_ClockConfigTypeDef sClockSourceConfig = {0};
  //TIM_MasterConfigTypeDef sMasterConfig = {0};

  /* USER CODE BEGIN TIM1_Init 1 */

  /* USER CODE END TIM1_Init 1 */
  htim1.Instance = TIM1;
  htim1.Init.Prescaler = 0;
  htim1.Init.CounterMode = TIM_COUNTERMODE_UP;
  htim1.Init.Period = 65535;
  htim1.Init.ClockDivision = TIM_CLOCKDIVISION_DIV1;
  htim1.Init.RepetitionCounter = 0;
  htim1.Init.AutoReloadPreload = TIM_AUTORELOAD_PRELOAD_DISABLE;
  if (HAL_TIM_Base_Init(&htim1) != HAL_OK)
  {
    Error_Handler();
  }
  //sClockSourceConfig.ClockSource = TIM_CLOCKSOURCE_INTERNAL;
  //if (HAL_TIM_ConfigClockSource(&htim1, &sClockSourceConfig) != HAL_OK)
  //{
  //  Error_Handler();
  //}
  //sMasterConfig.MasterOutputTrigger = TIM_TRGO_RESET;
  //sMasterConfig.MasterSlaveMode = TIM_MASTERSLAVEMODE_DISABLE;
  //if (HAL_TIMEx_MasterConfigSynchronization(&htim1, &sMasterConfig) != HAL_OK)
  //{
  //  Error_Handler();
  //}
  /* USER CODE BEGIN TIM1_Init 2 */

  /* USER CODE END TIM1_Init 2 */

}

/**
  * @brief GPIO Initialization Function
  * @param None
  * @retval None
  */
static void MX_GPIO_Init(void)
{
  GPIO_InitTypeDef GPIO_InitStruct = {0};
/* USER CODE BEGIN MX_GPIO_Init_1 */
/* USER CODE END MX_GPIO_Init_1 */

  /* GPIO Ports Clock Enable */
  __HAL_RCC_GPIOD_CLK_ENABLE();
  __HAL_RCC_GPIOA_CLK_ENABLE();

  /*Configure GPIO pin Output Level */
  HAL_GPIO_WritePin(GPIOA, GPIO_PIN_2|GPIO_PIN_3|GPIO_PIN_4, GPIO_PIN_RESET);

  // Настройка SCK, MOSI, MISO
   GPIO_InitStruct.Pin = GPIO_PIN_5 | GPIO_PIN_6 | GPIO_PIN_7;
   GPIO_InitStruct.Mode = GPIO_MODE_AF_PP;
   GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_HIGH;
   HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

  /*Configure GPIO pins : PA2 PA3 PA4 */
  GPIO_InitStruct.Pin = GPIO_PIN_2|GPIO_PIN_3|GPIO_PIN_4;
  GPIO_InitStruct.Mode = GPIO_MODE_OUTPUT_PP;
  GPIO_InitStruct.Pull = GPIO_PULLUP;
  GPIO_InitStruct.Speed = GPIO_SPEED_FREQ_LOW;
  HAL_GPIO_Init(GPIOA, &GPIO_InitStruct);

/* USER CODE BEGIN MX_GPIO_Init_2 */
/* USER CODE END MX_GPIO_Init_2 */
}

/* USER CODE BEGIN 4 */

// Функция инициализации USB
void CDC_Receive_FS(uint8_t* Buf, uint32_t *Len)
{
	 /*for (uint32_t i = 0; i < *Len; i++)
	 {
	        uint8_t c = Buf[i];
	        if (c == '\r' || c == '\n')
	        {
	            if (usb_rx_index == 6) // ждём ровно 6 символов
	            {
	                uint8_t b0 = hex2byte((char*)usb_cmd_buffer);
	                uint8_t b1 = hex2byte((char*)usb_cmd_buffer+2);
	                uint8_t b2 = hex2byte((char*)usb_cmd_buffer+4);

	                data = ((uint32_t)b0 << 16) | ((uint32_t)b1 << 8) | b2;
	            }
	            else
	            {
	                term_send("ERR\r\n");
	            }
	            usb_rx_index = 0;
	        }
	        else if (usb_rx_index < USB_RX_BUFFER_SIZE)
	        {
	        	usb_cmd_buffer[usb_rx_index++] = c;
	        }
	    }*/

	for (uint32_t i = 0; i < *Len; i++)
    {
        if (Buf[i] == '\n')
        {
            if (usb_rx_index >= 6)
            {
                // Конвертируем HEX строку в байты
                char hex_str[7];
                memcpy(hex_str, usb_rx_buffer, 6);
                hex_str[6] = '\0';

                for (int j = 0; j < 3; j++)
                {
                    char byte_str[3] = {hex_str[j*2], hex_str[j*2+1], '\0'};
                    usb_cmd_buffer[j] = (uint8_t)strtol(byte_str, NULL, 16);
                }

                usb_cmd_ready = 1;
            }
            usb_rx_index = 0;
        }
        else
        {
            if (usb_rx_index >= sizeof(usb_rx_buffer) - 1)
            {
                usb_rx_index = 0; // Сброс при переполнении
                continue; // Пропускаем этот байт
            }

            if (usb_rx_index < sizeof(usb_rx_buffer) - 1)
            {
                usb_rx_buffer[usb_rx_index++] = Buf[i];
            }
        }
    }

    // Продолжаем прием
    USBD_CDC_SetRxBuffer(&hUsbDeviceFS, usb_rx_buffer);
    USBD_CDC_ReceivePacket(&hUsbDeviceFS);
}

// Отправка ответа через USB
void send_response(const char *response)
{
char response_with_newline[32];
    int len = snprintf(response_with_newline, sizeof(response_with_newline), "%s\n", response);

    if (len > 0 && len < sizeof(response_with_newline))
    {
        USBD_StatusTypeDef status = CDC_Transmit_FS((uint8_t*)response_with_newline, len);

        if (status != USBD_OK)
        {
            // Можно добавить повторную попытку или логирование ошибки
            // Например, повторная попытка через 1 мс:
            HAL_Delay(1);
            CDC_Transmit_FS((uint8_t*)response_with_newline, len);
        }
    }
}

// Основной цикл обработки команд
void process_commands(void)
{
     if (usb_cmd_ready)
    {
        process_usb_command(usb_cmd_buffer);
        usb_cmd_ready = 0;
    }
}

void process_usb_command(uint8_t *command)
{
    // Команда сброса: 00 00 00
    if (command[0] == 0x00 && command[1] == 0x00 && command[2] == 0x00)
    {
        reset_chip();
        send_response("RESET_OK");
    }
    // Команда инициализации регистров: FF FF FF
    else if (command[0] == 0xFF && command[1] == 0xFF && command[2] == 0xFF)
    {
        init_control_registers();
        send_response("INIT_OK");
    }
    // Команда передачи данных: 01 XX XX
    else if (command[0] == 0x00 || command[0] == 0x01)
    {
        uint32_t data = (command[1] << 8) | command[2];
        transmit_24bit(data);
        send_response("TX_OK");
    }
    else if (command[0] == 0x02) // Команда чтения температуры
    {
        // Реализация полной процедуры чтения температуры по даташиту
        uint8_t chipId = 0; // Можно сделать параметром, если нужно
        uint32_t* temp_cmds = TRHJ1060_PrepareTemperatureReading(chipId);
        for (int i = 0; i < 3; i++)
        {
            transmit_24bit(temp_cmds[i]);
            delay_us(1);
        }
        // Задержка не менее 2400 тактов SCLK (примерно 40 мкс при 60 МГц SCLK)
        delay_us(50); // Подберите под вашу частоту SCLK
        // Чтение сигнального регистра (адрес 0x00)
        uint32_t sig_cmd = TRHJ1060_ReadSignalRegister(chipId, 0x00);
        uint8_t tx_data[3] = {
            (sig_cmd >> 16) & 0xFF,
            (sig_cmd >> 8) & 0xFF,
            sig_cmd & 0xFF
        };
        uint8_t rx_data[3] = {0};
        HAL_GPIO_WritePin(CS_PORT, CS_PIN, GPIO_PIN_RESET);
        HAL_SPI_TransmitReceive(&hspi1, tx_data, rx_data, 3, 100);
        HAL_GPIO_WritePin(CS_PORT, CS_PIN, GPIO_PIN_SET);
        // Парсинг ответа
        float temperature = TRHJ1060_ParseTemperatureResponse(rx_data);
        int temp_int = (int)temperature;
        int temp_frac = (int)((temperature - temp_int) * 10);
        char response[32];
        snprintf(response, sizeof(response), "TEMP:%d.%d", temp_int, temp_frac);
        send_response(response);
    }
    else
    {
        send_response("CMD_ERROR");
    }
}
// Сброс чипа
void reset_chip(void)
{
    HAL_GPIO_WritePin(RESET_PORT, RESET_PIN, GPIO_PIN_RESET);
    delay_us(10);
    HAL_GPIO_WritePin(RESET_PORT, RESET_PIN, GPIO_PIN_SET);
    delay_us(10);
}

// Инициализация контрольных регистров
void init_control_registers(void)
{
    for (int i = 0; i < 25; i++)
    {
        transmit_24bit(controlRegister[i]);
        delay_us(1); // Небольшая задержка между передачами
    }
}

void transmit_24bit(uint32_t data)
{
    // Правильная отправка 24-битного фрейма
    uint8_t tx_data[3];
    tx_data[0] = (data >> 16) & 0xFF; // Старший байт
    tx_data[1] = (data >> 8) & 0xFF;  // Средний байт
    tx_data[2] = data & 0xFF;         // Младший байт

    uint8_t rx_data[3];
    uint32_t result = 0;

    HAL_GPIO_WritePin(CS_PORT, CS_PIN, GPIO_PIN_RESET);
    HAL_SPI_TransmitReceive(&hspi1, tx_data, rx_data, 3, 100);
    HAL_GPIO_WritePin(CS_PORT, CS_PIN, GPIO_PIN_SET);
    HAL_SPI_Transmit(&hspi1, (uint8_t[]){0x00}, 1, 10);

    // Импульс Latch
    HAL_GPIO_WritePin(LATCH_PORT, LATCH_PIN, GPIO_PIN_RESET);
    delay_us(10);
    HAL_GPIO_WritePin(LATCH_PORT, LATCH_PIN, GPIO_PIN_SET);

    result = ((uint32_t)rx_data[0] << 16) |
             ((uint32_t)rx_data[1] << 8)  |
             rx_data[2];
}

static void term_send(const char *s)
{
    CDC_Transmit_FS((uint8_t*)s, strlen(s));
}

static uint8_t hex2nibble(char c)
{
    if (c >= '0' && c <= '9') return c - '0';
    if (c >= 'A' && c <= 'F') return c - 'A' + 10;
    if (c >= 'a' && c <= 'f') return c - 'a' + 10;
    return 0xFF;
}

static uint8_t hex2byte(const char *p)
{
    uint8_t hi = hex2nibble(*p);
    uint8_t lo = hex2nibble(*(p+1));
    if (hi > 15 || lo > 15) return 0xFF;
    return (hi << 4) | lo;
}

// Функции для работы с TRHJ-1060 (упрощенная реализация)
uint32_t* TRHJ1060_PrepareTemperatureReading(uint8_t chipId) {
    static uint32_t commands[3];

    // CTRL244 = 0x00 (temperature input)
    // Формат: ChipID(4) + Write(0) + ControlRegister(011) + Address(0xF4) + Data(0x00)
    commands[0] = ((uint32_t)chipId << 20) | (0u << 19) | (0b011u << 16) | (0xF4u << 8) | 0x00u;
    
    // CTRL16 = 0x8C (ADC enable + divider)
    // Address 0x10, Value 0x8C
    commands[1] = ((uint32_t)chipId << 20) | (0u << 19) | (0b011u << 16) | (0x10u << 8) | 0x8Cu;
    
    // CTRL245 = 0x20 (default calibration)
    // Address 0xF5, Value 0x20
    commands[2] = ((uint32_t)chipId << 20) | (0u << 19) | (0b011u << 16) | (0xF5u << 8) | 0x20u;
    
    return commands;
}

uint32_t TRHJ1060_ReadSignalRegister(uint8_t chipId, uint8_t address) {
    // Формат: ChipID(4) + Read(1) + SignalRegister(101) + Address + Data(0)
    return ((uint32_t)chipId << 20) | (1u << 19) | (0b101u << 16) | ((uint32_t)address << 8);
}

float TRHJ1060_ParseTemperatureResponse(uint8_t* response) {
    if (response == NULL) return -273.0f; // Абсолютный ноль при ошибке
    
    // Извлекаем 10-битное значение температуры (биты D8-D17 ответного фрейма)
    // response[0] - старший байт, response[1] - средний, response[2] - младший
    uint16_t adcValue = ((uint16_t)(response[1] & 0x3F) << 4) | ((uint16_t)(response[2] & 0xF0) >> 4);

    // Формула из документации: Temperature = 249 - 0.364 * Code
    return 249.0f - 0.364f * adcValue;
}
/* USER CODE END 4 */

/**
  * @brief  This function is executed in case of error occurrence.
  * @retval None
  */
void Error_Handler(void)
{
  /* USER CODE BEGIN Error_Handler_Debug */
  /* User can add his own implementation to report the HAL error return state */
  __disable_irq();
  while (1)
  {
  }
  /* USER CODE END Error_Handler_Debug */
}

#ifdef  USE_FULL_ASSERT
/**
  * @brief  Reports the name of the source file and the source line number
  *         where the assert_param error has occurred.
  * @param  file: pointer to the source file name
  * @param  line: assert_param error line source number
  * @retval None
  */
void assert_failed(uint8_t *file, uint32_t line)
{
  /* USER CODE BEGIN 6 */
  /* User can add his own implementation to report the file name and line number,
     ex: printf("Wrong parameters value: file %s on line %d\r\n", file, line) */
  /* USER CODE END 6 */
}
#endif /* USE_FULL_ASSERT */
