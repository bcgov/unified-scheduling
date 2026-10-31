<template>
  <footer class="app-footer">
    <p>Access to modules and features depends on your assigned role and permissions.</p>
  </footer>
</template>

<style scoped>
.app-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex: 0 0 auto;
  flex-wrap: wrap;
  gap: 12px 24px;
  padding: 16px var(--ua-page-padding-x);
  border-top: 1px solid #d5dfed;
  background: #fff;
  color: var(--ua-brand-muted);
  font-family: var(--ua-brand-font);
  font-size: 13px;
  line-height: 1.5;
}

.app-footer p {
  margin: 0 0 0 auto;
  text-align: right;
}

@media (max-width: 600px) {
  .app-footer p {
    margin-left: 0;
    text-align: left;
  }
}
</style>
