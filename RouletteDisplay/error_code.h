#ifndef __ERROR_CODE_H_
#define __ERROR_CODE_H_


/* -----------------------------------------------
    ENUM
   ----------------------------------------------- */
enum ERROR_CODE {
	ERR_NONE = 0,
	ERR_DEV_NOT_INIT						= 0x100,		// Call API without initialize library
	ERR_INVALID_PARAM						= 0x101,		// Invalid parameter
	ERR_FAIL_ALLOC							= 0x102,		// Could not allocate memory
	ERR_FAIL_DEALLOC						= 0x103,		// Could not deallocate memory
	ERR_NO_DEV								= 0x104,		// The device is not found or not support
	ERR_NO_PERN								= 0x105,		// No permission 
	ERR_DRV_FAIL							= 0x106,		// System call fail
	ERR_IOC_NO_IMPL							= 0x107,		// Non implement Ioctl message

	ERR_INTR_IS_ENABLED						= 0x200,		// Interrupt is enabled
	ERR_INTR_IS_DISABLED					= 0x201,		// Interrupt is disabled
	ERR_INTR_IS_REGISTERED					= 0x202,		// Interrupt is registered
	ERR_INTR_IS_UNREGISTERED				= 0x203,		// Interrupt is unregistered

	ERR_AES_TIMEOUT							= 0x300,		// Timeout for handling the AES command
	ERR_SPI_TIMEOUT							= 0x301,		// Timeout for handling the SPI command

	ERR_I2C_BUSY							= 0x400,		// SMBUS/I2C bus is busy
	ERR_I2C_TIMEOUT							= 0x401,		// SMBUS/I2C transaction timeout
	ERR_I2C_TRANS_FAIL						= 0x402,		// Someone kill SMBUS transaction
	ERR_I2C_BUS_ERR							= 0x403,		// SMBUS/I2C bus collision
	ERR_I2C_DEV_ERR							= 0x404,		// SMBUS/I2C slave device error
	ERR_I2C_NO_SUP							= 0x405,		// Reserved
	ERR_I2C_NO_DATA							= 0x406,		// SMBUS/I2C slave device error

	ERR_PIC_DATA_INVALID					= 0x500,		// PIC data format is not correct
	ERR_PIC_COMM_FAIL						= 0x501,		// PIC response 0xE0
	ERR_PIC_DATA_CRC_FAIL					= 0x502,		// Reserved
	ERR_PIC_LOG_EMPTY						= 0x503,		// PIC data is empty
	
	ERR_PIC_NOT_SUPPORT						= 0x504,		// This function not support on PIC old architecture
	ERR_PMU_NOT_SUPPORT						= 0x505,		// This function not support on PIC new architecture

//== Error code from PIC (for new architecture of PIC)========================================
	ERR_PMU_PKG_FORMAT						= 0x511,
	ERR_PMU_PKG_CS							= 0x512,
	ERR_PMU_PKG_LEN							= 0x513,
	ERR_PMU_SYSTEM_IS_BUSY					= 0x514,	

	ERR_PMU_UNKOWN_CMD						= 0x517,
	ERR_PMU_WRONG_PARAM						= 0x518,
	ERR_PMU_CMD_NOT_SUPPORT					= 0x519,		

	ERR_PMU_NO_RECORD						= 0x521,		// PMU data is empty
	ERR_PMU_CLR_LOG							= 0x522,
	ERR_PMU_WRONG_EVENT_TYPE				= 0x523,
	ERR_PMU_ID_CRC							= 0x531,
	ERR_PMU_DEV_AVAIL						= 0x532,
	ERR_PMU_INT_ENABLED						= 0x541,
	ERR_PMU_INT_DISABLED					= 0x542,
	ERR_PMU_INT_NOT_ENA						= 0x543,
	ERR_PMU_INT_NO_EVENT_HAPPENED			= 0x544,
	ERR_PMU_OLD_IS_RUNNING					= 0x551,
	ERR_PMU_NOT_IN_OLD						= 0x552,	
//===========================================================================================
	ERR_SPI_COMM_FAIL						= 0x600,		// SPI communication is not correct
	ERR_SPI_SEC_WRONG_RSP					= 0x601,
	ERR_SPI_SEC_WRONG_CS					= 0x602,
	ERR_SPI_SEC_RSP_NAK						= 0x603,
	ERR_SPI_SEC_ERR_CS						= 0x604,
	ERR_SPI_SEC_ERR_BUSY					= 0x605,
	ERR_SPI_SEC_ERR_DATA					= 0x606,
	ERR_SPI_SEC_ERR_CMD						= 0x607,
	ERR_SPI_SEC_ERR_EEPROM_RW				= 0x608,
	ERR_SPI_SEC_ERR_FP_NOT_SET				= 0x609,
	ERR_SPI_SEC_ERR_FW						= 0x60A,
	ERR_SPI_SEC_ERR_EEPROM_BUS				= 0x60B,
	ERR_SPI_SEC_ERR_RX_OVERFLOW				= 0x60C,
	ERR_SPI_SEC_ERR_ONE_EEPROM_CS			= 0x60D,
	ERR_SPI_SEC_ERR_TWO_EEPROM_CS			= 0x60E,
	ERR_SPI_SEC_ERR_TIMEOUT					= 0x60F,
	ERR_SPI_SEC_ERR_UNKNOWN					= 0x610,
	
	ERR_SAS_NO_RESPONSE						= 0x3001,		// No response from GXG SAS processor
	ERR_SAS_NO_ROM_SIG_REQUEST				= 0x4001,
	ERR_SAS_GAME_START_WHILE_GAME_MENU		= 0x4002,
	ERR_SAS_WAGER_EXCEED_MAX_BET			= 0x4003,
	ERR_SAS_EXCEED_IMPLEMENT_GAME_NUM		= 0x4004,
	ERR_SAS_RTC_NO_SYNC						= 0x4005,
	ERR_SAS_ROM_SIG_SEED_NOT_MATCH			= 0x4006,

	ERR_SAS_MD_NOT_SUP						= 0x4801,
	ERR_SAS_MD_SAS_DENOM_NOT_SET			= 0x4802,
	ERR_SAS_MD_NOT_MULTI_OF_SAS_DENOM		= 0x4803,
	ERR_SAS_MD_CODE_IS_ENABLED				= 0x4804,
	ERR_SAS_MD_CODE_IS_DISABLED				= 0x4805,
	ERR_SAS_MD_CODE_ALREADY_ADD				= 0x4806,
	ERR_SAS_MD_CODE_NOT_PRESENT				= 0x4807,
	ERR_SAS_MD_CODE_IS_USED					= 0x4808,

	ERR_SAS_LEGACY_BONUS_AMOUNT_NOT_MATCH	= 0x5001,
	ERR_SAS_LEGACY_BONUS_NOT_CFG			= 0x5002,

	ERR_SAS_HANDPAY_BUSY					= 0x5801,
	ERR_SAS_HANDPAY_ALREADY_RESET			= 0x5802,
	ERR_SAS_HANDPAY_RECEIPT_NOT_CFG			= 0x5803,

	ERR_SAS_PREP_PRINT_BUSY					= 0x6001,
	ERR_SAS_PREP_PRINT_NO_VALID_SEC_ID		= 0x6002,
	ERR_SAS_PREP_PRINT_LINK_DOWN			= 0x6003,
	ERR_SAS_PREP_PRINT_NOT_ALLOW			= 0x6004,

	ERR_SAS_SEC_VALID_NUM_NOT_SET			= 0x6401,

	ERR_SAS_REDEEM_PRE_NOT_FINISH			= 0x6801,
	ERR_SAS_REDEEM_WHILE_LINK_DOWN			= 0x6802,
	ERR_SAS_REDEEM_NOT_CFG					= 0x6803,
	ERR_SAS_REDEEM_ALREADY_REJECT			= 0x6804,
	ERR_SAS_REDEEM_NOT_ALLOW_FINISH			= 0x6805,

	ERR_SAS_AFT_NOT_SUP						= 0x7001,
	ERR_SAS_AFT_REG_ZERO_ASSET_NUM			= 0x7002,
	ERR_SAS_AFT_REG_BUSY					= 0x7003,
	ERR_SAS_AFT_REG_NO_PERN					= 0x7004,
	ERR_SAS_AFT_LOCK_CONDITION_NOT_MATCH	= 0x7011,
	ERR_SAS_AFT_LOCK_NO_PERN				= 0x7012,
	ERR_SAS_AFT_TRANS_HOST_CANCELLED		= 0x7021,
	ERR_SAS_AFT_TRANS_NO_PERN				= 0x7022,
	ERR_SAS_AFT_TRANS_NO_PENDING_DATA		= 0x7023,
	ERR_SAS_AFT_TRANS_AMOUNT_NOT_MATCH		= 0x7024,
	ERR_SAS_AFT_TRANS_AMOUNT_EXCEED_LIMIT	= 0x7025,
	ERR_SAS_AFT_TRANS_HOST_CASHOUT_NOT_ENABLED = 0x7026,
	ERR_SAS_AFT_TRANS_BUSY					= 0x7027,

	ERR_SAS_PROG_QUEUE_IS_NOT_EMPTY			= 0x8001,
	ERR_SAS_PROG_NO_ANY_ENABLED_LEVEL		= 0x8002,
	ERR_SAS_PROG_IS_NOT_ENABLED				= 0x8003,
	ERR_SAS_PROG_HIT_AMOUNT_NOT_MATCH		= 0x8004,

	ERR_SAS_NVRAM_DATA_CORRUPT				= 0xF001,

	ERR_SAS_TXRX_MSG_QUEUE_EMPTY			= 0xF801,
	ERR_SAS_NOT_ENABLE_TRACE_TXRX			= 0xF802,
	
	// For driver/api internal used
	ERR_INTR_QUEUE_EMPTY = 0xFF000,
};


#endif
