<template>
  <section class="calendar-matrix-skeleton" aria-busy="true" aria-label="Loading calendar matrix">
    <div class="calendar-matrix-skeleton__table" role="presentation">
      <div class="calendar-matrix-skeleton__row calendar-matrix-skeleton__row--header">
        <v-skeleton-loader
          v-for="column in 7"
          :key="`header-${column}`"
          class="calendar-matrix-skeleton__loader"
          :class="`calendar-matrix-skeleton__loader--column-${column}`"
          type="heading"
        />
      </div>

      <div v-for="row in 3" :key="`row-${row}`" class="calendar-matrix-skeleton__row">
        <v-skeleton-loader
          v-for="column in 7"
          :key="`row-${row}-column-${column}`"
          class="calendar-matrix-skeleton__loader"
          :class="`calendar-matrix-skeleton__loader--column-${column}`"
          type="text"
        />
      </div>
    </div>
  </section>
</template>

<style scoped>
.calendar-matrix-skeleton {
  padding: var(--ua-spacing-lg) 0 0;
}

.calendar-matrix-skeleton__table {
  border: 1px solid var(--ua-border-color);
  border-radius: var(--ua-border-radius);
  overflow: auto;
}

.calendar-matrix-skeleton__row {
  display: grid;
  grid-template-columns:
    minmax(13.75rem, 16.25rem)
    repeat(5, minmax(9rem, 1fr))
    minmax(15rem, 18.75rem);
  min-width: 74rem;
}

.calendar-matrix-skeleton__row + .calendar-matrix-skeleton__row {
  border-top: 1px solid var(--ua-border-color);
}

.calendar-matrix-skeleton__row--header {
  background: var(--ua-calendar-panel-bg);
}

.calendar-matrix-skeleton__row--header .calendar-matrix-skeleton__loader {
  min-height: 3rem;
  padding-block: var(--ua-spacing-sm);
}

.calendar-matrix-skeleton__loader {
  border-right: 1px solid var(--ua-border-color);
  min-height: 7rem;
  padding: var(--ua-spacing-md);
}

.calendar-matrix-skeleton__loader:last-child {
  border-right: 0;
}

:deep(.v-skeleton-loader__heading),
:deep(.v-skeleton-loader__text) {
  margin: 0;
}

.calendar-matrix-skeleton__row--header :deep(.v-skeleton-loader__heading) {
  height: 0.875rem;
  width: 60%;
}

.calendar-matrix-skeleton__loader--column-1 :deep(.v-skeleton-loader__heading),
.calendar-matrix-skeleton__loader--column-7 :deep(.v-skeleton-loader__heading) {
  width: 45%;
}

.calendar-matrix-skeleton__loader--column-1 :deep(.v-skeleton-loader__text),
.calendar-matrix-skeleton__loader--column-7 :deep(.v-skeleton-loader__text) {
  width: 78%;
}

.calendar-matrix-skeleton__loader:not(.calendar-matrix-skeleton__loader--column-1):not(
    .calendar-matrix-skeleton__loader--column-7
  )
  :deep(.v-skeleton-loader__text) {
  height: 1.75rem;
}
</style>
